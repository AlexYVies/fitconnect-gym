using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MembresiasController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public MembresiasController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(new { items = await _db.Membresias.ToListAsync() });

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _db.Membresias.FindAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "admin_general")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Membresia item)
    {
        _db.Membresias.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "admin_general")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Membresia item)
    {
        var existing = await _db.Membresias.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Nombre = item.Nombre;
        existing.Precio = item.Precio;
        existing.DuracionDias = item.DuracionDias;
        existing.Descripcion = item.Descripcion;
        existing.Estatus = item.Estatus;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize(Roles = "admin_general")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Membresias.FindAsync(id);
        if (item == null) return NotFound();
        _db.Membresias.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
