using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public UsuariosController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.Usuarios.Include(u => u.Sucursal).AsQueryable();
        if (rol == "admin_sucursal" && idSuc != null)
            query = query.Where(u => u.IdSucursal == int.Parse(idSuc));

        return Ok(new { items = await query.Select(u => new
        {
            u.Id, u.Nombre, u.Email, u.Rol, u.IdSucursal, u.Estatus,
            sucursal = u.Sucursal != null ? u.Sucursal.Nombre : null
        }).ToListAsync() });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _db.Usuarios.FindAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "admin_general,admin_sucursal")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Usuario item)
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        if (rol == "admin_sucursal")
        {
            if (item.Rol == "admin_general") return Forbid();   // no escalar privilegios
            if (idSuc != null) item.IdSucursal = int.Parse(idSuc); // solo su sede
        }
        _db.Usuarios.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "admin_general,admin_sucursal")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Usuario item)
    {
        var existing = await _db.Usuarios.FindAsync(id);
        if (existing == null) return NotFound();

        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        if (rol == "admin_sucursal")
        {
            if (idSuc == null || existing.IdSucursal != int.Parse(idSuc)) return Forbid(); // solo su sede
            if (item.Rol == "admin_general") return Forbid();
            item.IdSucursal = int.Parse(idSuc); // no moverlo a otra sede
        }

        existing.Nombre = item.Nombre;
        existing.Email = item.Email;
        if (!string.IsNullOrEmpty(item.Password)) existing.Password = item.Password;
        existing.Rol = item.Rol;
        existing.IdSucursal = item.IdSucursal;
        existing.Estatus = item.Estatus;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    [Authorize(Roles = "admin_general,admin_sucursal")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Usuarios.FindAsync(id);
        if (item == null) return NotFound();
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        if (rol == "admin_sucursal" && (idSuc == null || item.IdSucursal != int.Parse(idSuc))) return Forbid();
        _db.Usuarios.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
