using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NfcLecturasController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public NfcLecturasController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        // Cada kiosko solo ve las lecturas de SU sucursal (el ESP32 manda su idSucursal).
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        var query = _db.NfcLecturas.AsQueryable();
        if (rol != "admin_general" && idSuc != null)
            query = query.Where(n => n.IdSucursal == int.Parse(idSuc));
        return Ok(new { items = await query.OrderByDescending(n => n.Timestamp).Take(50).ToListAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] NfcLectura item)
    {
        item.Timestamp = DateTime.Now;
        _db.NfcLecturas.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [AllowAnonymous]
    [HttpPost("por-uid")]
    public async Task<IActionResult> CreateOrUpdateByUid([FromBody] NfcLectura item)
    {
        var existing = await _db.NfcLecturas.FirstOrDefaultAsync(n => n.Uid == item.Uid);
        if (existing != null)
        {
            existing.Timestamp = DateTime.Now;
            existing.IdSucursal = item.IdSucursal;
        }
        else
        {
            item.Timestamp = DateTime.Now;
            _db.NfcLecturas.Add(item);
        }
        await _db.SaveChangesAsync();
        return Ok(existing ?? item);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.NfcLecturas.FindAsync(id);
        if (item == null) return NotFound();
        _db.NfcLecturas.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
