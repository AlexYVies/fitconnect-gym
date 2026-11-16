import network
import ujson
import neopixel
from machine import Pin, PWM, time_pulse_us
from time import sleep_ms, sleep_us, ticks_ms, ticks_diff
from mfrc522 import MFRC522
from umqtt.simple import MQTTClient   # si falta: import mip; mip.install("umqtt.simple")

# =============================================
# CONFIGURACION - CAMBIAR ESTOS VALORES
# =============================================
WIFI_SSID = "YOUR_WIFI_SSID"
WIFI_PASS = "YOUR_WIFI_PASSWORD"

# Broker MQTT (corre en la VM Debian / servidor FitConnect, en la misma LAN).
# OJO: la IP de la VM es por DHCP y puede cambiar al reiniciar el router/VM.
# Verificala en la VM con 'hostname -I' y actualizala aqui.
MQTT_BROKER = "192.168.1.100"
MQTT_PORT   = 1883
ID_SUCURSAL = 1                 # Central = 1

CLIENT_ID    = "esp32-sucursal-{}".format(ID_SUCURSAL)
TOPIC_NFC    = "fitconnect/{}/nfc".format(ID_SUCURSAL)       # publica UID
TOPIC_SENSOR = "fitconnect/{}/sensor".format(ID_SUCURSAL)    # publica datos de sensores
TOPIC_ACTUAR = "fitconnect/{}/actuador".format(ID_SUCURSAL)  # se suscribe (comandos/resultado)

UMBRAL_PRESENCIA_CM = 60        # a menos de esto, se considera "presencia"
NUM_LEDS = 12                   # <-- AJUSTA al numero de LEDs de tu anillo WS2812B (8/12/16/24)

# =============================================
# HARDWARE - PINES ESP32
# =============================================
# --- RC522 (NFC, SPI) ---
reader = MFRC522(sck=18, mosi=23, miso=19, rst=22, cs=5)

# --- HC-SR04 #1 (entrada) y #2 (salida) ---  Echo por divisor 5V->3.3V
trig1 = Pin(13, Pin.OUT); echo1 = Pin(34, Pin.IN)
trig2 = Pin(14, Pin.OUT); echo2 = Pin(35, Pin.IN)

# --- Pulsador (boton de salida manual) ---
boton = Pin(27, Pin.IN, Pin.PULL_UP)   # presionado = 0

# --- Servo MG95 de ROTACION CONTINUA (torniquete) --- 5V EXTERNO + GND comun
servo = PWM(Pin(26), freq=50)
SERVO_STOP  = 1500     # detenido
SERVO_ABRE  = 1700     # gira en un sentido (abrir)
SERVO_CIERRA = 1300    # gira en sentido contrario (cerrar)

# --- Anillo LED WS2812B (direccionable) --- DIN a GPIO25, alimentar a 5V
ring = neopixel.NeoPixel(Pin(25), NUM_LEDS)
VERDE = (0, 60, 0); ROJO = (60, 0, 0); AZUL = (0, 0, 60); NEGRO = (0, 0, 0)
MORADO = (50, 0, 50)   # salida por boton

# --- Buzzer PASIVO (necesita PWM para sonar; el pin solo en alto no hace ruido) ---
buzzer = PWM(Pin(21), freq=2000, duty_u16=0)

# =============================================
# HELPERS DE ACTUADORES
# =============================================
def ring_color(rgb):
    for i in range(NUM_LEDS):
        ring[i] = rgb
    ring.write()

# Estados del anillo:
#   "arcoiris" = en espera (animacion girando)
#   "azul"     = procesando (enviando el UID del RC522 al backend)
#   "fijo"     = verde/rojo (controlado por acceso_ok / denegado)
ring_modo = "arcoiris"
ring_hasta = 0            # ticks_ms para volver a arcoiris (0 = sin pendiente)
hue = 0
ult_arcoiris = 0

def _rueda(pos):
    # pos 0..255 -> color del arcoiris (atenuado a ~20% de brillo)
    pos &= 255
    if pos < 85:
        r, g, b = pos * 3, 255 - pos * 3, 0
    elif pos < 170:
        pos -= 85; r, g, b = 255 - pos * 3, 0, pos * 3
    else:
        pos -= 170; r, g, b = 0, pos * 3, 255 - pos * 3
    return (r // 5, g // 5, b // 5)

def paso_arcoiris():
    global hue, ult_arcoiris
    if ticks_diff(ticks_ms(), ult_arcoiris) < 40:   # velocidad del giro
        return
    ult_arcoiris = ticks_ms()
    for i in range(NUM_LEDS):
        ring[i] = _rueda(i * 256 // NUM_LEDS + hue)
    ring.write()
    hue = (hue + 4) & 255

def ring_arcoiris():
    global ring_modo, ring_hasta
    ring_modo = "arcoiris"; ring_hasta = 0

def ring_azul_proceso():
    global ring_modo, ring_hasta
    ring_color(AZUL); ring_modo = "azul"
    ring_hasta = ticks_ms() + 4000       # si no llega respuesta, vuelve a arcoiris

def ring_fijo(color, revert_ms=0):
    global ring_modo, ring_hasta
    ring_color(color); ring_modo = "fijo"
    ring_hasta = (ticks_ms() + revert_ms) if revert_ms else 0

def revisar_ring():
    if ring_modo == "arcoiris":
        paso_arcoiris()
    elif ring_hasta and ticks_diff(ticks_ms(), ring_hasta) >= 0:
        ring_arcoiris()

def beep(ms=80, veces=1, pausa=80, freq=2000):
    # Buzzer pasivo: se genera un tono con PWM. freq = tono (Hz).
    for i in range(veces):
        buzzer.freq(freq)
        buzzer.duty_u16(32768)   # 50% de duty = suena
        sleep_ms(ms)
        buzzer.duty_u16(0)       # silencio
        if i < veces - 1:
            sleep_ms(pausa)

def mover(pulso_us):
    servo.duty_u16(pulso_us * 65535 // 20000)

# --- Torniquete controlado por el ultrasonico (no bloqueante) ---
# Al abrir, el ultrasonico de esa entrada mide la distancia BASE (pasillo vacio,
# primer pulso). Cuando alguien entra al haz la distancia cambia; cuando vuelve a
# ser ~igual a la base, la persona ya termino de pasar y ENTONCES se cierra.
EST_IDLE, EST_ABRIENDO, EST_ABIERTO, EST_CERRANDO = 0, 1, 2, 3
serv_estado = EST_IDLE
serv_t = 0
abierto_desde = 0
GIRO_MS = 700           # cuanto gira el servo para abrir/cerrar
MAX_OPEN_MS = 10000     # cierre de seguridad si nadie termina de pasar
TOL_CM = 8              # "casi igual" a la distancia base
CAMBIO_CM = 15          # cuanto debe cambiar para contar como persona en el haz

paso_trig = None
paso_echo = None
paso_tipo = None
d_base = None           # distancia del primer pulso (pasillo vacio)
cambio_detectado = False
ult_med_paso = 0

def abrir_torniquete(evento=None, sensor_cierre=None):
    # evento: etiqueta que se guarda en el backend ("ultrasonico_entrada"/"..._salida").
    # sensor_cierre: que ultrasonico vigila el paso para cerrar -> "entrada" (13/34) o "salida" (14/35).
    #   Al ENTRAR se cierra tras el sensor de SALIDA; al SALIR tras el de ENTRADA.
    global serv_estado, serv_t, paso_trig, paso_echo, paso_tipo, d_base, cambio_detectado
    mover(SERVO_ABRE); serv_estado = EST_ABRIENDO; serv_t = ticks_ms()
    d_base = None; cambio_detectado = False
    if evento:
        paso_tipo = evento
        paso_trig, paso_echo = (trig1, echo1) if sensor_cierre == "entrada" else (trig2, echo2)
    else:
        paso_tipo = None    # comando manual: cierra por tiempo de seguridad

def revisar_servo():
    global serv_estado, serv_t, abierto_desde, d_base, cambio_detectado, ult_med_paso
    if serv_estado == EST_IDLE:
        return

    if serv_estado == EST_ABRIENDO:
        if ticks_diff(ticks_ms(), serv_t) >= GIRO_MS:
            mover(SERVO_STOP); serv_estado = EST_ABIERTO; abierto_desde = ticks_ms()
        return

    if serv_estado == EST_ABIERTO:
        cerrar = ticks_diff(ticks_ms(), abierto_desde) >= MAX_OPEN_MS   # seguridad
        if not cerrar and paso_tipo and ticks_diff(ticks_ms(), ult_med_paso) >= 120:
            ult_med_paso = ticks_ms()
            d = medir_cm(paso_trig, paso_echo)
            if d > 0:
                if d_base is None:
                    d_base = d                        # primer pulso = distancia base
                    print("Distancia base: %.1f cm" % d_base)
                elif not cambio_detectado:
                    if abs(d - d_base) > CAMBIO_CM:    # alguien entro al haz
                        cambio_detectado = True
                        pub(TOPIC_SENSOR, {"tipo": paso_tipo, "valor": round(d, 1)})
                        print("Persona pasando (%s): %.1f cm (base %.1f)" % (paso_tipo, d, d_base))
                elif abs(d - d_base) <= TOL_CM:        # volvio a la base -> ya termino de pasar
                    cerrar = True
                    print("Persona termino de pasar -> cerrar")
        if cerrar:
            mover(SERVO_CIERRA); serv_estado = EST_CERRANDO; serv_t = ticks_ms()
        return

    if serv_estado == EST_CERRANDO:
        if ticks_diff(ticks_ms(), serv_t) >= GIRO_MS:
            mover(SERVO_STOP); serv_estado = EST_IDLE
            ring_arcoiris()      # volver a la animacion de espera al cerrar

def acceso_ok():
    ring_fijo(VERDE)         # verde fijo mientras el torniquete esta abierto
    beep(70, 2, 70, freq=2500)   # dos pitidos AGUDOS = ACCESO PERMITIDO
    abrir_torniquete("ultrasonico_entrada", "salida")   # entra: cierra tras pasar el sensor de SALIDA

def acceso_denegado():
    ring_fijo(ROJO, 3000)    # rojo 3s y vuelve a la animacion de espera
    beep(600, 1, freq=400)   # un tono GRAVE largo = ACCESO DENEGADO

# =============================================
# HC-SR04
# =============================================
def medir_cm(trig, echo):
    trig.value(0); sleep_us(2)
    trig.value(1); sleep_us(10); trig.value(0)
    try:
        dur = time_pulse_us(echo, 1, 30000)   # timeout 30 ms (~5 m)
    except OSError:
        return -1
    return -1 if dur < 0 else dur / 58.0

# =============================================
# WIFI + MQTT
# =============================================
wlan = None
mqtt = None

def conectar_wifi():
    global wlan
    wlan = network.WLAN(network.STA_IF)
    # Reinicia la interfaz para evitar "Wifi Internal State Error" tras soft-reboot
    wlan.active(False); sleep_ms(150)
    wlan.active(True);  sleep_ms(200)
    if wlan.isconnected():
        print("WiFi:", wlan.ifconfig()[0]); return wlan
    print("Conectando WiFi:", WIFI_SSID)
    try:
        wlan.disconnect()
    except Exception:
        pass
    try:
        wlan.connect(WIFI_SSID, WIFI_PASS)
    except OSError as e:
        print("WiFi ocupado, reiniciando interfaz:", e)
        wlan.active(False); sleep_ms(400); wlan.active(True); sleep_ms(300)
        wlan.connect(WIFI_SSID, WIFI_PASS)
    n = 0
    while not wlan.isconnected() and n < 30:
        sleep_ms(500); n += 1
    print("WiFi:", wlan.ifconfig()[0] if wlan.isconnected() else "SIN CONEXION")
    return wlan

def on_actuador(topic, msg):
    # Respuesta de acceso del backend, o comando manual desde el dashboard.
    try:
        d = ujson.loads(msg)
    except Exception:
        return
    accion = d.get("accion")
    if accion == "acceso":
        if d.get("estado") == "ok":
            print("  [MQTT] Acceso PERMITIDO"); acceso_ok()
        else:
            print("  [MQTT] Acceso DENEGADO"); acceso_denegado()
    elif accion == "comando":
        act, est = d.get("actuador"), d.get("estado")
        print("  [MQTT] Comando dashboard:", act, est)
        if act == "buzzer":
            beep(150, 1)
        elif act == "servo":
            abrir_torniquete()
        elif act == "led_verde":
            ring_fijo(VERDE, 3000) if est == "on" else ring_arcoiris()
        elif act == "led_rojo":
            ring_fijo(ROJO, 3000) if est == "on" else ring_arcoiris()

def conectar_mqtt():
    global mqtt
    mqtt = MQTTClient(CLIENT_ID, MQTT_BROKER, port=MQTT_PORT, keepalive=60)
    mqtt.set_callback(on_actuador)
    mqtt.connect()
    mqtt.subscribe(TOPIC_ACTUAR)
    print("MQTT conectado a {}:{}".format(MQTT_BROKER, MQTT_PORT))

def pub(topic, obj):
    try:
        mqtt.publish(topic, ujson.dumps(obj)); return True
    except Exception as e:
        print("Error MQTT publish:", e); return False

# =============================================
# LOOP PRINCIPAL
# =============================================
def main():
    print("\n=== FitConnect - Kiosko ESP32 (NFC + 2x Ultrasonico + Boton / Servo continuo + Anillo WS2812B + Buzzer) ===")
    print("Sucursal ID:", ID_SUCURSAL)
    mover(SERVO_STOP); ring_arcoiris()

    if not conectar_wifi().isconnected():
        print("Sin WiFi. Reinicia."); ring_color(ROJO); beep(200, 3, 120); return
    conectar_mqtt()
    beep(60, 2, 60)          # dos pitidos = sistema listo
    print("Listo. Acerca un llavero...\n")

    ultimo_uid, ultimo_uid_t = "", 0
    boton_prev = 1
    ult_ping = ticks_ms()

    while True:
        # 1) Comandos entrantes (actuadores)
        try:
            mqtt.check_msg()
            # Ping periodico para que el broker no cierre la conexion estando inactiva
            if ticks_diff(ticks_ms(), ult_ping) > 20000:
                ult_ping = ticks_ms(); mqtt.ping()
        except Exception as e:
            print("MQTT caido, reconectando...", e)
            try:
                conectar_wifi(); conectar_mqtt(); ult_ping = ticks_ms()
            except Exception:
                sleep_ms(2000)

        # 2) Estados no bloqueantes: servo (con deteccion de paso) y anillo
        revisar_servo()
        revisar_ring()

        # 3) NFC
        stat, _ = reader.request(reader.REQIDL)
        if stat == reader.OK:
            stat, raw = reader.anticoll()
            if stat == reader.OK:
                uid = "{:02X} {:02X} {:02X} {:02X}".format(raw[0], raw[1], raw[2], raw[3])
                if not (uid == ultimo_uid and ticks_diff(ticks_ms(), ultimo_uid_t) < 5000):
                    ultimo_uid, ultimo_uid_t = uid, ticks_ms()
                    print("Llavero:", uid); beep(30, 1)   # tick corto = llavero leido
                    ring_azul_proceso()            # AZUL = enviando el UID al backend
                    pub(TOPIC_NFC, {"uid": uid})   # el backend responde por TOPIC_ACTUAR

        # 4) Pulsador (boton de salida manual): flanco de presion.
        #    Abre el servo y activa la ventana del ultrasonico de SALIDA.
        b = boton.value()
        if b == 0 and boton_prev == 1:
            print("Boton de salida presionado -> abrir")
            pub(TOPIC_SENSOR, {"tipo": "boton", "valor": 1})
            beep(60, 1); ring_fijo(MORADO); abrir_torniquete("ultrasonico_salida", "entrada")   # sale: cierra tras pasar el sensor de ENTRADA
        boton_prev = b

        sleep_ms(60)


print("Iniciando en 3s... (pulsa Ctrl-C ahora si quieres subir codigo)")
sleep_ms(3000) 
try:
    main()
except KeyboardInterrupt:
    print("Detenido por el usuario")
finally:
    # Dejar todo en reposo al parar el codigo
    ring_color(NEGRO)        # apagar el anillo LED
    buzzer.duty_u16(0)       # silenciar el buzzer
    servo.duty_u16(0)        # soltar el servo
