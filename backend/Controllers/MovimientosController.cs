using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/movimientos")]
[Authorize]
public class MovimientosController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public MovimientosController(FitConnectDbContext db) => _db = db;

    // Todos pueden VER el historial (recepcionista/admin_sucursal: solo su sede).
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? idProducto)
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.MovimientosInventario.Include(m => m.Producto).AsQueryable();
        if (rol != "admin_general" && idSuc != null)
            query = query.Where(m => m.IdSucursal == int.Parse(idSuc));
        if (idProducto != null)
            query = query.Where(m => m.IdProducto == idProducto);

        return Ok(new { items = await query.OrderByDescending(m => m.Fecha).Take(200).Select(m => new
        {
            m.Id, m.IdProducto, m.IdSucursal, m.Tipo, m.Cantidad, m.Motivo, m.IdUsuario, m.Fecha,
            producto = m.Producto != null ? m.Producto.Nombre : null
        }).ToListAsync() });
    }

    public class MovimientoRequest
    {
        public int IdProducto { get; set; }
        public string Tipo { get; set; } = "entrada";   // entrada | baja
        public int Cantidad { get; set; }
        public string? Motivo { get; set; }
    }

    // Mover inventario: solo el admin_sucursal, y solo el de su propia sede.
    [Authorize(Roles = "admin_sucursal")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MovimientoRequest req)
    {
        if (req.Cantidad <= 0) return BadRequest(new { error = "La cantidad debe ser mayor a 0" });
        if (req.Tipo != "entrada" && req.Tipo != "baja") return BadRequest(new { error = "Tipo invalido" });

        var producto = await _db.Productos.FindAsync(req.IdProducto);
        if (producto == null) return NotFound(new { error = "Producto no encontrado" });

        var idSucStr = User.FindFirst("idSucursal")?.Value;
        if (idSucStr == null) return Forbid();
        var idSuc = int.Parse(idSucStr);

        // Existencia de este producto en MI sucursal (se crea si no existe).
        var stockRow = await _db.StockSucursal.FirstOrDefaultAsync(s => s.IdProducto == producto.Id && s.IdSucursal == idSuc);
        if (stockRow == null)
        {
            stockRow = new StockSucursal { IdProducto = producto.Id, IdSucursal = idSuc, Stock = 0 };
            _db.StockSucursal.Add(stockRow);
        }

        if (req.Tipo == "baja" && stockRow.Stock < req.Cantidad)
            return BadRequest(new { error = "No hay stock suficiente para la baja" });

        stockRow.Stock += req.Tipo == "entrada" ? req.Cantidad : -req.Cantidad;

        var uid = int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var u) ? u : (int?)null;
        var mov = new MovimientoInventario
        {
            IdProducto = producto.Id, IdSucursal = idSuc,
            Tipo = req.Tipo, Cantidad = req.Cantidad, Motivo = req.Motivo, IdUsuario = uid
        };
        _db.MovimientosInventario.Add(mov);
        await _db.SaveChangesAsync();
        return Ok(new { producto = producto.Id, idSucursal = idSuc, stock = stockRow.Stock, movimiento = mov.Id });
    }
}
