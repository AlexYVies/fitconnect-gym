using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientesController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public ClientesController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.Clientes.Include(c => c.Sucursal).AsQueryable();
        if (rol != "admin_general" && idSuc != null)
            query = query.Where(c => c.IdSucursal == int.Parse(idSuc));

        var clientes = await query.ToListAsync();
        var clienteIds = clientes.Select(c => c.Id).ToList();
        var ultimosPagos = await _db.Pagos
            .Include(p => p.Membresia)
            .Where(p => clienteIds.Contains(p.IdCliente))
            .GroupBy(p => p.IdCliente)
            .Select(g => g.OrderByDescending(p => p.Fecha).First())
            .ToListAsync();

        var result = clientes.Select(c =>
        {
            var pago = ultimosPagos.FirstOrDefault(p => p.IdCliente == c.Id);
            return new
            {
                c.Id, c.Nombre, c.Telefono, c.Email, c.Foto, c.UidNfc, c.EstatusNfc,
                c.IdSucursal, c.FechaRegistro, c.Estatus,
                sucursal = c.Sucursal?.Nombre,
                membresia = pago?.Membresia?.Nombre,
                vigencia = c.FechaVigencia,
                vigente = c.FechaVigencia != null && c.FechaVigencia >= DateTime.Today
            };
        });
        return Ok(new { items = result });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var item = await _db.Clientes.FindAsync(id);
        return item == null ? NotFound() : Ok(item);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Cliente item)
    {
        if (string.IsNullOrEmpty(item.Password) && !string.IsNullOrEmpty(item.Email))
        {
            var at = item.Email.IndexOf('@');
            item.Password = at > 0 ? item.Email[..at] : item.Email;
        }
        _db.Clientes.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] Cliente item)
    {
        var existing = await _db.Clientes.FindAsync(id);
        if (existing == null) return NotFound();
        existing.Nombre = item.Nombre;
        existing.Telefono = item.Telefono;
        existing.Email = item.Email;
        existing.Foto = item.Foto;
        existing.UidNfc = item.UidNfc;
        existing.EstatusNfc = item.EstatusNfc;
        existing.IdSucursal = item.IdSucursal;
        existing.Estatus = item.Estatus;
        if (!string.IsNullOrEmpty(item.Password)) existing.Password = item.Password;
        await _db.SaveChangesAsync();
        return Ok(existing);
    }

    public class CambioSucursalRequest { public int IdSucursal { get; set; } }

    // El cliente cambia su propia sucursal, SOLO si su membresia esta vencida.
    [Authorize(Roles = "cliente")]
    [HttpPut("mi-sucursal")]
    public async Task<IActionResult> CambiarMiSucursal([FromBody] CambioSucursalRequest req)
    {
        var idCli = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var cliente = await _db.Clientes.FindAsync(idCli);
        if (cliente == null) return NotFound();
        var vigente = cliente.FechaVigencia.HasValue && cliente.FechaVigencia.Value >= DateTime.Today;
        if (vigente) return BadRequest(new { error = "Solo puedes cambiar de sucursal con la membresia vencida." });
        var suc = await _db.Sucursales.FindAsync(req.IdSucursal);
        if (suc == null) return BadRequest(new { error = "Sucursal no encontrada" });
        cliente.IdSucursal = req.IdSucursal;
        await _db.SaveChangesAsync();
        return Ok(new { idSucursal = cliente.IdSucursal, sucursal = suc.Nombre });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Clientes.FindAsync(id);
        if (item == null) return NotFound();
        _db.Clientes.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
