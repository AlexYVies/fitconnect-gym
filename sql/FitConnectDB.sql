-- =============================================
-- FitConnect Pro - SQL Server Database
-- =============================================

CREATE DATABASE FitConnectDB;
GO
USE FitConnectDB;
GO

-- =============================================
-- TABLAS
-- =============================================

CREATE TABLE Sucursales (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL UNIQUE,
    Direccion NVARCHAR(500),
    Telefono NVARCHAR(20),
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'activa'
);

CREATE TABLE Usuarios (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(150) NOT NULL,
    Email NVARCHAR(200) NOT NULL UNIQUE,
    Password NVARCHAR(200) NOT NULL,
    Rol NVARCHAR(30) NOT NULL CHECK (Rol IN ('admin_general','admin_sucursal','recepcionista','kiosko')),
    IdSucursal INT NULL REFERENCES Sucursales(Id),
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'activo'
);

CREATE TABLE Clientes (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(150) NOT NULL,
    Telefono NVARCHAR(20),
    Email NVARCHAR(200),
    Foto NVARCHAR(500),
    UidNfc NVARCHAR(50),
    EstatusNfc NVARCHAR(20) NOT NULL DEFAULT 'sin_asignar',
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    FechaRegistro DATE NOT NULL DEFAULT GETDATE(),
    FechaVigencia DATE NULL,
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'activo',
    Password NVARCHAR(200)
);

CREATE TABLE Membresias (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(100) NOT NULL UNIQUE,
    Precio DECIMAL(10,2) NOT NULL,
    DuracionDias INT NOT NULL,
    Descripcion NVARCHAR(500),
    AccesoTodasSedes BIT NOT NULL DEFAULT 0,
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'activa'
);

CREATE TABLE Pagos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdCliente INT NOT NULL REFERENCES Clientes(Id),
    IdMembresia INT NOT NULL REFERENCES Membresias(Id),
    Monto DECIMAL(10,2) NOT NULL,
    Metodo NVARCHAR(30) NOT NULL CHECK (Metodo IN ('efectivo','tarjeta','transferencia')),
    IdRecepcionista INT NOT NULL REFERENCES Usuarios(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    Fecha DATE NOT NULL DEFAULT GETDATE(),
    Notas NVARCHAR(500)
);

CREATE TABLE Asistencias (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdCliente INT NOT NULL REFERENCES Clientes(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    UidNfc NVARCHAR(50),
    FechaHora DATETIME2 NOT NULL DEFAULT GETDATE(),
    Metodo NVARCHAR(20) NOT NULL DEFAULT 'manual' CHECK (Metodo IN ('manual','nfc','nfc_movil'))
);

-- Producto GLOBAL (el stock vive por sucursal en StockSucursal)
CREATE TABLE Productos (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Nombre NVARCHAR(150) NOT NULL,
    Precio DECIMAL(10,2) NOT NULL,
    Categoria NVARCHAR(50),
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'activo'
);

CREATE TABLE StockSucursal (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdProducto INT NOT NULL REFERENCES Productos(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    Stock INT NOT NULL DEFAULT 0,
    CONSTRAINT UX_StockSucursal UNIQUE (IdProducto, IdSucursal)
);

CREATE TABLE Ventas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdProducto INT NOT NULL REFERENCES Productos(Id),
    Cantidad INT NOT NULL,
    PrecioUnitario DECIMAL(10,2) NOT NULL,
    Total DECIMAL(10,2) NOT NULL,
    IdCliente INT NULL REFERENCES Clientes(Id),
    IdRecepcionista INT NOT NULL REFERENCES Usuarios(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    Metodo NVARCHAR(30) NOT NULL CHECK (Metodo IN ('efectivo','tarjeta','transferencia')),
    Fecha DATE NOT NULL DEFAULT GETDATE()
);

CREATE TABLE CortesCaja (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdRecepcionista INT NOT NULL REFERENCES Usuarios(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    FechaApertura DATETIME2 NOT NULL DEFAULT GETDATE(),
    FechaCierre DATETIME2 NULL,
    TotalMembresias DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalProductos DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalEfectivo DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalTarjeta DECIMAL(10,2) NOT NULL DEFAULT 0,
    TotalGeneral DECIMAL(10,2) NOT NULL DEFAULT 0,
    Estatus NVARCHAR(20) NOT NULL DEFAULT 'abierto'
);

CREATE TABLE NfcLecturas (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Uid NVARCHAR(50) NOT NULL,
    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
    IdSucursal INT NULL REFERENCES Sucursales(Id)
);

CREATE TABLE LecturasSensor (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Tipo NVARCHAR(40) NOT NULL,
    Valor FLOAT NOT NULL DEFAULT 0,
    IdSucursal INT NULL REFERENCES Sucursales(Id),
    Timestamp DATETIME2 NOT NULL DEFAULT GETDATE()
);

CREATE TABLE MovimientosInventario (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    IdProducto INT NOT NULL REFERENCES Productos(Id),
    IdSucursal INT NOT NULL REFERENCES Sucursales(Id),
    Tipo NVARCHAR(20) NOT NULL CHECK (Tipo IN ('entrada','baja','venta')),
    Cantidad INT NOT NULL,
    Motivo NVARCHAR(300),
    IdUsuario INT NULL REFERENCES Usuarios(Id),
    Fecha DATETIME2 NOT NULL DEFAULT GETDATE()
);

-- =============================================
-- INDICES
-- =============================================
CREATE INDEX IX_MovInv_IdProducto ON MovimientosInventario(IdProducto);
CREATE INDEX IX_MovInv_IdSucursal ON MovimientosInventario(IdSucursal);
CREATE INDEX IX_MovInv_Fecha ON MovimientosInventario(Fecha);
CREATE INDEX IX_Clientes_UidNfc ON Clientes(UidNfc);
CREATE INDEX IX_Clientes_Email ON Clientes(Email);
CREATE INDEX IX_Clientes_IdSucursal ON Clientes(IdSucursal);
CREATE INDEX IX_Pagos_IdCliente ON Pagos(IdCliente);
CREATE INDEX IX_Pagos_Fecha ON Pagos(Fecha);
CREATE INDEX IX_Asistencias_IdCliente ON Asistencias(IdCliente);
CREATE INDEX IX_Asistencias_FechaHora ON Asistencias(FechaHora);
CREATE INDEX IX_Ventas_Fecha ON Ventas(Fecha);
CREATE INDEX IX_NfcLecturas_Uid ON NfcLecturas(Uid);
CREATE INDEX IX_LecturasSensor_Timestamp ON LecturasSensor(Timestamp);

-- =============================================
-- DATOS INICIALES
-- =============================================

INSERT INTO Sucursales (Nombre, Direccion, Estatus) VALUES ('Central', 'Direccion de la sucursal central', 'activa');

INSERT INTO Usuarios (Nombre, Email, Password, Rol, IdSucursal, Estatus)
VALUES ('Administrador', 'admin@fitconnect.com', 'admin123', 'admin_general', NULL, 'activo');

INSERT INTO Usuarios (Nombre, Email, Password, Rol, IdSucursal, Estatus)
VALUES ('Admin Central', 'admin.central@fitconnect.com', 'central123', 'admin_sucursal', 1, 'activo');

INSERT INTO Membresias (Nombre, Precio, DuracionDias, Estatus) VALUES ('Basica', 499.00, 30, 'activa');
INSERT INTO Membresias (Nombre, Precio, DuracionDias, AccesoTodasSedes, Estatus) VALUES ('Premium', 899.00, 30, 1, 'activa');

GO
PRINT 'Base de datos FitConnectDB creada correctamente.';
GO
