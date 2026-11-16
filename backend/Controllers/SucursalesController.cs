using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SucursalesController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public SucursalesController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { items = await _db.Sucursales.ToListAsync() });

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _db.Sucursales.FindAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "admin_general")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Sucursal item)
    {
        _db.Sucursales.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "admin_general")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Sucursal item)
    {
        var existing = await _db.Sucursales.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Nombre = item.Nombre;
        existing.Direccion = item.Direccion;
        existing.Telefono = item.Telefono;
        existing.Estatus = item.Estatus;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize(Roles = "admin_general")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Sucursales.FindAsync(id);
        if (item == null) return NotFound();
        _db.Sucursales.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
