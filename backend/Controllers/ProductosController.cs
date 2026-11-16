using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductosController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public ProductosController(FitConnectDbContext db) => _db = db;

    // Productos GLOBALES. El campo "stock" depende del rol:
    //   admin_general -> total sumando todas las sucursales
    //   admin_sucursal / recepcionista -> stock de SU sucursal
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        int? sucId = (rol != "admin_general" && idSuc != null) ? int.Parse(idSuc) : null;

        var productos = await _db.Productos.ToListAsync();
        var stocks = await _db.StockSucursal.ToListAsync();

        var result = productos.Select(p => new
        {
            p.Id, p.Nombre, p.Precio, p.Categoria, p.Estatus,
            stock = sucId != null
                ? stocks.Where(s => s.IdProducto == p.Id && s.IdSucursal == sucId).Sum(s => s.Stock)
                : stocks.Where(s => s.IdProducto == p.Id).Sum(s => s.Stock)
        });
        return Ok(new { items = result });
    }

    // Desglose de stock por sucursal de un producto (para el boton "Ver stock").
    [HttpGet("{id}/stock")]
    public async Task<IActionResult> Stock(int id)
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.StockSucursal.Include(s => s.Sucursal).Where(s => s.IdProducto == id);
        if (rol != "admin_general" && idSuc != null)
            query = query.Where(s => s.IdSucursal == int.Parse(idSuc));

        return Ok(new { items = await query
            .OrderBy(s => s.IdSucursal)
            .Select(s => new { s.IdSucursal, s.Stock, sucursal = s.Sucursal != null ? s.Sucursal.Nombre : null })
            .ToListAsync() });
    }

    // Solo el general crea productos. Nacen con stock 0 en TODAS las sucursales.
    [Authorize(Roles = "admin_general")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Producto item)
    {
        _db.Productos.Add(item);
        await _db.SaveChangesAsync();

        var sucursales = await _db.Sucursales.Select(s => s.Id).ToListAsync();
        foreach (var sid in sucursales)
            _db.StockSucursal.Add(new StockSucursal { IdProducto = item.Id, IdSucursal = sid, Stock = 0 });
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "admin_general")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Producto item)
    {
        var existing = await _db.Productos.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Nombre = item.Nombre;
        existing.Precio = item.Precio;
        existing.Categoria = item.Categoria;
        existing.Estatus = item.Estatus;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize(Roles = "admin_general")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Productos.FindAsync(id);
        if (item == null) return NotFound();
        if (await _db.Ventas.AnyAsync(v => v.IdProducto == id))
            return BadRequest(new { error = "No se puede eliminar: el producto tiene ventas registradas. Marcalo como inactivo." });
        _db.MovimientosInventario.RemoveRange(_db.MovimientosInventario.Where(m => m.IdProducto == id));
        _db.StockSucursal.RemoveRange(_db.StockSucursal.Where(s => s.IdProducto == id));
        _db.Productos.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
