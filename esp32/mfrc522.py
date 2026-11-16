# mfrc522.py - Driver MFRC522 para MicroPython / ESP32 (robusto)
# Version con SoftSPI (bit-bang): funciona en cualquier pin y es mas tolerante.
# Basado en danjperron/micropython-mfrc522 (MIT).

from machine import Pin, SoftSPI
from time import sleep_ms


class MFRC522:

    OK = 0
    NOTAGERR = 1
    ERR = 2

    REQIDL = 0x26
    REQALL = 0x52
    AUTHENT1A = 0x60
    AUTHENT1B = 0x61

    def __init__(self, sck, mosi, miso, rst, cs, baudrate=100000):
        self.sck = Pin(sck, Pin.OUT)
        self.mosi = Pin(mosi, Pin.OUT)
        self.miso = Pin(miso, Pin.IN)
        self.rst = Pin(rst, Pin.OUT)
        self.cs = Pin(cs, Pin.OUT)

        self.rst.value(0)
        self.cs.value(1)

        # SoftSPI: SPI por software, sirve en cualquier pin.
        self.spi = SoftSPI(baudrate=baudrate, polarity=0, phase=0,
                           sck=self.sck, mosi=self.mosi, miso=self.miso)

        self.rst.value(1)
        sleep_ms(50)
        self.init()

    # ---------------- registros ----------------
    def _wreg(self, reg, val):
        self.cs.value(0)
        self.spi.write(bytes([(reg << 1) & 0x7E, val & 0xFF]))
        self.cs.value(1)

    def _rreg(self, reg):
        self.cs.value(0)
        self.spi.write(bytes([((reg << 1) & 0x7E) | 0x80]))
        val = self.spi.read(1)
        self.cs.value(1)
        return val[0]

    def _sflags(self, reg, mask):
        self._wreg(reg, self._rreg(reg) | mask)

    def _cflags(self, reg, mask):
        self._wreg(reg, self._rreg(reg) & (~mask & 0xFF))

    def version(self):
        # VersionReg (0x37): 0x91/0x92 = MFRC522 genuino comunicando bien.
        # 0x00 o 0xFF = SIN comunicacion SPI (cableado/pines/modulo danado).
        return self._rreg(0x37)

    # ---------------- transceiver ----------------
    def _tocard(self, cmd, send):
        recv = []
        bits = 0
        stat = self.ERR
        irq_en = 0x00
        wait_irq = 0x00

        if cmd == 0x0E:          # MFAuthent
            irq_en = 0x12
            wait_irq = 0x10
        elif cmd == 0x0C:        # Transceive
            irq_en = 0x77
            wait_irq = 0x30

        self._wreg(0x02, irq_en | 0x80)   # ComIEnReg
        self._cflags(0x04, 0x80)          # ComIrqReg: limpiar
        self._sflags(0x0A, 0x80)          # FIFOLevelReg: FlushBuffer
        self._wreg(0x01, 0x00)            # CommandReg: Idle

        for c in send:
            self._wreg(0x09, c)           # FIFODataReg

        self._wreg(0x01, cmd)             # ejecutar comando

        if cmd == 0x0C:
            self._sflags(0x0D, 0x80)      # BitFramingReg: StartSend

        i = 2000
        n = 0
        while True:
            n = self._rreg(0x04)          # ComIrqReg
            i -= 1
            if not (i and not (n & 0x01) and not (n & wait_irq)):
                break

        self._cflags(0x0D, 0x80)          # limpiar StartSend

        if i:
            if (self._rreg(0x06) & 0x1B) == 0x00:   # ErrorReg
                stat = self.OK
                if n & irq_en & 0x01:
                    stat = self.NOTAGERR
                elif cmd == 0x0C:
                    n = self._rreg(0x0A)            # FIFOLevelReg
                    lbits = self._rreg(0x0C) & 0x07  # ControlReg
                    if lbits != 0:
                        bits = (n - 1) * 8 + lbits
                    else:
                        bits = n * 8
                    if n == 0:
                        n = 1
                    elif n > 16:
                        n = 16
                    for _ in range(n):
                        recv.append(self._rreg(0x09))
            else:
                stat = self.ERR

        return stat, recv, bits

    def _crc(self, data):
        self._cflags(0x05, 0x04)
        self._sflags(0x0A, 0x80)
        for c in data:
            self._wreg(0x09, c)
        self._wreg(0x01, 0x03)
        i = 0xFF
        while True:
            n = self._rreg(0x05)
            i -= 1
            if not (i and not (n & 0x04)):
                break
        return [self._rreg(0x22), self._rreg(0x21)]

    # ---------------- init / antena ----------------
    def init(self):
        self.reset()
        self._wreg(0x2A, 0x8D)   # TModeReg
        self._wreg(0x2B, 0x3E)   # TPrescalerReg
        self._wreg(0x2D, 30)     # TReloadReg low
        self._wreg(0x2C, 0)      # TReloadReg high
        self._wreg(0x15, 0x40)   # TxASKReg: 100% ASK
        self._wreg(0x11, 0x3D)   # ModeReg
        self.antenna_on()

    def reset(self):
        self._wreg(0x01, 0x0F)   # SoftReset
        sleep_ms(50)

    def antenna_on(self, on=True):
        if on and not (self._rreg(0x14) & 0x03):
            self._sflags(0x14, 0x03)
        elif not on:
            self._cflags(0x14, 0x03)

    # ---------------- API ----------------
    def request(self, mode):
        self._wreg(0x0D, 0x07)   # BitFramingReg: 7 bits
        (stat, recv, bits) = self._tocard(0x0C, [mode])
        if (stat != self.OK) | (bits != 0x10):
            stat = self.ERR
        return stat, bits

    def anticoll(self):
        ser_chk = 0
        ser = [0x93, 0x20]
        self._wreg(0x0D, 0x00)
        (stat, recv, bits) = self._tocard(0x0C, ser)
        if stat == self.OK:
            if len(recv) == 5:
                for i in range(4):
                    ser_chk = ser_chk ^ recv[i]
                if ser_chk != recv[4]:
                    stat = self.ERR
            else:
                stat = self.ERR
        return stat, recv

    def select_tag(self, ser):
        buf = [0x93, 0x70] + ser[:5]
        buf += self._crc(buf)
        (stat, recv, bits) = self._tocard(0x0C, buf)
        return self.OK if (stat == self.OK) and (bits == 0x18) else self.ERR

    def auth(self, mode, addr, sect, ser):
        return self._tocard(0x0E, [mode, addr] + sect + ser[:4])[0]

    def stop_crypto1(self):
        self._cflags(0x08, 0x08)

    def read(self, addr):
        data = [0x30, addr]
        data += self._crc(data)
        (stat, recv, _) = self._tocard(0x0C, data)
        return recv if stat == self.OK else None

    def write(self, addr, data):
        buf = [0xA0, addr]
        buf += self._crc(buf)
        (stat, recv, bits) = self._tocard(0x0C, buf)
        if not (stat == self.OK) or not (bits == 4) or not ((recv[0] & 0x0F) == 0x0A):
            stat = self.ERR
        else:
            buf = []
            for i in range(16):
                buf.append(data[i])
            buf += self._crc(buf)
            (stat, recv, bits) = self._tocard(0x0C, buf)
            if not (stat == self.OK) or not (bits == 4) or not ((recv[0] & 0x0F) == 0x0A):
                stat = self.ERR
        return stat


# ------------------------------------------------------------------
# Para volver a SPI por HARDWARE (mas rapido), en __init__ cambia:
#   from machine import Pin, SoftSPI     ->   from machine import Pin, SPI
#   self.spi = SoftSPI(baudrate=..., sck=self.sck, mosi=self.mosi, miso=self.miso)
#   ->  self.spi = SPI(2, baudrate=1000000, polarity=0, phase=0,
#                      sck=self.sck, mosi=self.mosi, miso=self.miso)
# ------------------------------------------------------------------
