using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class VentasController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public VentasController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.Ventas
            .Include(v => v.Producto)
            .Include(v => v.Cliente)
            .Include(v => v.Recepcionista)
            .Include(v => v.Sucursal)
            .AsQueryable();

        if (rol != "admin_general" && idSuc != null)
            query = query.Where(v => v.IdSucursal == int.Parse(idSuc));

        return Ok(new { items = await query.Select(v => new
        {
            v.Id, v.IdProducto, v.Cantidad, v.PrecioUnitario, v.Total,
            v.IdCliente, v.IdRecepcionista, v.IdSucursal, v.Metodo, v.Fecha,
            producto = v.Producto != null ? v.Producto.Nombre : null,
            cliente = v.Cliente != null ? v.Cliente.Nombre : null,
            recepcionista = v.Recepcionista != null ? v.Recepcionista.Nombre : null,
            sucursal = v.Sucursal != null ? v.Sucursal.Nombre : null
        }).OrderByDescending(v => v.Fecha).ToListAsync() });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Venta item)
    {
        var producto = await _db.Productos.FindAsync(item.IdProducto);
        if (producto == null) return BadRequest(new { error = "Producto no encontrado" });

        var stockRow = await _db.StockSucursal.FirstOrDefaultAsync(s => s.IdProducto == item.IdProducto && s.IdSucursal == item.IdSucursal);
        if (stockRow == null || stockRow.Stock < item.Cantidad)
            return BadRequest(new { error = "Stock insuficiente" });

        stockRow.Stock -= item.Cantidad;
        item.PrecioUnitario = producto.Precio;
        item.Total = producto.Precio * item.Cantidad;

        _db.Ventas.Add(item);
        await _db.SaveChangesAsync();

        // La venta queda registrada como movimiento de inventario (salida).
        var uid = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) ? u : (int?)null;
        _db.MovimientosInventario.Add(new MovimientoInventario
        {
            IdProducto = producto.Id, IdSucursal = item.IdSucursal,
            Tipo = "venta", Cantidad = item.Cantidad, Motivo = "Venta", IdUsuario = uid
        });
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost("lote")]
    public async Task<IActionResult> CreateBatch([FromBody] VentaLoteRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest(new { error = "El carrito de venta está vacío" });

        if (request.Items.Any(i => i.Cantidad <= 0))
            return BadRequest(new { error = "La cantidad de cada producto debe ser mayor a cero" });

        var idSucursal = request.IdSucursal
            ?? (int.TryParse(User.FindFirst("idSucursal")?.Value, out var sucClaim) ? sucClaim : 0);
        if (idSucursal <= 0)
            return BadRequest(new { error = "Sucursal inválida" });

        var uid = int.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var u) ? u : (int?)null;
        var idRecepcionista = request.IdRecepcionista ?? uid ?? 0;
        if (idRecepcionista <= 0)
            return BadRequest(new { error = "Recepcionista inválido" });
        var metodo = string.IsNullOrWhiteSpace(request.Metodo) ? "efectivo" : request.Metodo;

        // Consolidar cantidades por producto para validar stock acumulado
        var agrupados = request.Items
            .GroupBy(i => i.IdProducto)
            .Select(g => new { IdProducto = g.Key, Cantidad = g.Sum(x => x.Cantidad) })
            .ToList();

        await using var tx = await _db.Database.BeginTransactionAsync();
        var creadas = new List<Venta>();
        decimal totalGeneral = 0;

        foreach (var linea in agrupados)
        {
            var producto = await _db.Productos.FindAsync(linea.IdProducto);
            if (producto == null)
                return BadRequest(new { error = $"Producto #{linea.IdProducto} no encontrado" });

            var stockRow = await _db.StockSucursal.FirstOrDefaultAsync(
                s => s.IdProducto == linea.IdProducto && s.IdSucursal == idSucursal);
            if (stockRow == null || stockRow.Stock < linea.Cantidad)
                return BadRequest(new { error = $"Stock insuficiente para {producto.Nombre} (disponible: {stockRow?.Stock ?? 0})" });

            stockRow.Stock -= linea.Cantidad;
            var subtotal = producto.Precio * linea.Cantidad;
            totalGeneral += subtotal;

            var venta = new Venta
            {
                IdProducto = producto.Id,
                Cantidad = linea.Cantidad,
                PrecioUnitario = producto.Precio,
                Total = subtotal,
                IdCliente = request.IdCliente,
                IdRecepcionista = idRecepcionista,
                IdSucursal = idSucursal,
                Metodo = metodo
            };
            _db.Ventas.Add(venta);
            creadas.Add(venta);

            _db.MovimientosInventario.Add(new MovimientoInventario
            {
                IdProducto = producto.Id,
                IdSucursal = idSucursal,
                Tipo = "venta",
                Cantidad = linea.Cantidad,
                Motivo = "Venta",
                IdUsuario = uid
            });
        }

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { count = creadas.Count, total = totalGeneral, items = creadas });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Ventas.FindAsync(id);
        if (item == null) return NotFound();
        _db.Ventas.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}

public record VentaLoteItemRequest(int IdProducto, int Cantidad);
public record VentaLoteRequest(List<VentaLoteItemRequest> Items, string Metodo, int? IdRecepcionista, int? IdSucursal, int? IdCliente);
