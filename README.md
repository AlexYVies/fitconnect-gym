# FitConnect — Smart Gym Management & IoT Access Control

**Full-Stack Gym Administration, Multi-Item POS & ESP32 NFC/MQTT Access Control System**  
Built with **.NET 8 (ASP.NET Core + EF Core)**, **SQL Server**, **MQTT (Mosquitto)**, **MicroPython (ESP32)**, **Docker**, and **HTML5/JavaScript**.

> 🇪🇸 **Resumen en Español:** Sistema integral para administración de gimnasios con control de acceso físico mediante **ESP32 + NFC (MFRC522) + MQTT**, punto de venta multi-artículo con control de inventario por sucursal, membresías, cortes de caja y despliegue con **Docker** / **IIS**.

---

## Architecture & Components

- **Backend (`backend/` — .NET 8 Web API):**
  - Role-based JWT authentication (`admin_general`, `admin_sucursal`, `recepcionista`).
  - Multi-branch inventory, transactional batch POS (`POST /api/ventas/lote`), membership payments, cash register cuts (`CortesCaja`), attendance, and financial reports.
  - Real-time MQTT integration (`MqttService.cs`) to validate NFC scans and trigger turnstile actuators.
- **IoT Firmware (`esp32/` — MicroPython):**
  - Reads NFC cards (`mfrc522.py`), monitors entry/exit ultrasonic sensors (`HC-SR04`), controls turnstile servo (`MG95`), status NeoPixel LED ring, and buzzer over MQTT.
- **Database (`sql/FitConnectDB.sql`):**
  - Relational SQL Server schema for branches, users, clients, memberships, inventory, sales, and access logs.
- **Deployment (`deploy/`):**
  - `Dockerfile`, `docker-compose.yml`, `nginx`, and `mosquitto` configuration.

---

## Quick Start

1. **Database:** Run `sql/FitConnectDB.sql` on SQL Server.
2. **Backend:** Update `ConnectionStrings:DefaultConnection` and `Jwt:Key` in `backend/appsettings.json`, then run:
   ```bash
   dotnet restore FitConnect.sln
   dotnet run --project backend/FitConnectAPI.csproj
   ```
3. **ESP32:** Configure `WIFI_SSID`, `WIFI_PASS`, and `MQTT_BROKER` in `esp32/main.py` and flash to your ESP32.
4. **Docker (optional):** Copy `deploy/.env.example` to `deploy/.env` and run `docker compose -f deploy/docker-compose.yml up -d`.
