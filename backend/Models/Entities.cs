using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace FitConnectAPI.Models;

public class Sucursal
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";
    public string? Direccion { get; set; }
    public string? Telefono { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "activa";
}

public class Usuario
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";
    [Required, MaxLength(200)]
    public string Email { get; set; } = "";
    [Required, MaxLength(200)]
    public string Password { get; set; } = "";
    [Required, MaxLength(30)]
    public string Rol { get; set; } = "recepcionista";
    public int? IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "activo";
}

public class Cliente
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? Foto { get; set; }
    public string? UidNfc { get; set; }
    [MaxLength(20)]
    public string EstatusNfc { get; set; } = "sin_asignar";
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.Today;
    // Fecha en la que vence la membresia. Los pagos SUMAN dias sobre esta fecha.
    public DateTime? FechaVigencia { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "activo";
    public string? Password { get; set; }
}

public class Membresia
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string Nombre { get; set; } = "";
    [Column(TypeName = "decimal(10,2)")]
    public decimal Precio { get; set; }
    public int DuracionDias { get; set; }
    public string? Descripcion { get; set; }
    // Si es true, el socio puede entrar a CUALQUIER sucursal (no solo la suya).
    public bool AccesoTodasSedes { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "activa";
}

public class Pago
{
    public int Id { get; set; }
    public int IdCliente { get; set; }
    [ForeignKey("IdCliente")]
    [JsonIgnore]
    public Cliente? Cliente { get; set; }
    public int IdMembresia { get; set; }
    [ForeignKey("IdMembresia")]
    [JsonIgnore]
    public Membresia? Membresia { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal Monto { get; set; }
    [Required, MaxLength(30)]
    public string Metodo { get; set; } = "efectivo";
    public int IdRecepcionista { get; set; }
    [ForeignKey("IdRecepcionista")]
    [JsonIgnore]
    public Usuario? Recepcionista { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Today;
    public string? Notas { get; set; }
}

public class Asistencia
{
    public int Id { get; set; }
    public int IdCliente { get; set; }
    [ForeignKey("IdCliente")]
    [JsonIgnore]
    public Cliente? Cliente { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public string? UidNfc { get; set; }
    public DateTime FechaHora { get; set; } = DateTime.Now;
    [MaxLength(20)]
    public string Metodo { get; set; } = "manual";
}

// Producto GLOBAL (no pertenece a una sucursal; el stock vive en StockSucursal).
public class Producto
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Nombre { get; set; } = "";
    [Column(TypeName = "decimal(10,2)")]
    public decimal Precio { get; set; }
    [MaxLength(50)]
    public string? Categoria { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "activo";
}

// Existencia de un producto en una sucursal concreta.
public class StockSucursal
{
    public int Id { get; set; }
    public int IdProducto { get; set; }
    [ForeignKey("IdProducto")]
    [JsonIgnore]
    public Producto? Producto { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public int Stock { get; set; }
}

public class Venta
{
    public int Id { get; set; }
    public int IdProducto { get; set; }
    [ForeignKey("IdProducto")]
    [JsonIgnore]
    public Producto? Producto { get; set; }
    public int Cantidad { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal PrecioUnitario { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal Total { get; set; }
    public int? IdCliente { get; set; }
    [ForeignKey("IdCliente")]
    [JsonIgnore]
    public Cliente? Cliente { get; set; }
    public int IdRecepcionista { get; set; }
    [ForeignKey("IdRecepcionista")]
    [JsonIgnore]
    public Usuario? Recepcionista { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    [MaxLength(30)]
    public string Metodo { get; set; } = "efectivo";
    public DateTime Fecha { get; set; } = DateTime.Today;
}

public class CorteCaja
{
    public int Id { get; set; }
    public int IdRecepcionista { get; set; }
    [ForeignKey("IdRecepcionista")]
    [JsonIgnore]
    public Usuario? Recepcionista { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public DateTime FechaApertura { get; set; } = DateTime.Now;
    public DateTime? FechaCierre { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalMembresias { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalProductos { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalEfectivo { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalTarjeta { get; set; }
    [Column(TypeName = "decimal(10,2)")]
    public decimal TotalGeneral { get; set; }
    [MaxLength(20)]
    public string Estatus { get; set; } = "abierto";
}

public class NfcLectura
{
    public int Id { get; set; }
    [Required, MaxLength(50)]
    public string Uid { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int? IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
}

// Lecturas de los sensores fisicos del kiosko (ultrasonico, boton) enviadas por MQTT.
public class LecturaSensor
{
    public int Id { get; set; }
    [Required, MaxLength(40)]
    public string Tipo { get; set; } = "";   // ultrasonico_entrada | ultrasonico_salida | boton
    public double Valor { get; set; }
    public int? IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class MovimientoInventario
{
    public int Id { get; set; }
    public int IdProducto { get; set; }
    [ForeignKey("IdProducto")]
    [JsonIgnore]
    public Producto? Producto { get; set; }
    public int IdSucursal { get; set; }
    [ForeignKey("IdSucursal")]
    [JsonIgnore]
    public Sucursal? Sucursal { get; set; }
    [Required, MaxLength(20)]
    public string Tipo { get; set; } = "entrada";   // entrada | baja | venta
    public int Cantidad { get; set; }
    [MaxLength(300)]
    public string? Motivo { get; set; }
    public int? IdUsuario { get; set; }
    public DateTime Fecha { get; set; } = DateTime.Now;
}
