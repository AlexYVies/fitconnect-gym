using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PagosController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public PagosController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.Pagos
            .Include(p => p.Cliente)
            .Include(p => p.Membresia)
            .Include(p => p.Recepcionista)
            .Include(p => p.Sucursal)
            .AsQueryable();

        if (rol == "cliente")
        {
            var idCli = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            query = query.Where(p => p.IdCliente == idCli);
        }
        else if (rol != "admin_general" && idSuc != null)
        {
            query = query.Where(p => p.IdSucursal == int.Parse(idSuc));
        }

        return Ok(new { items = await query.Select(p => new
        {
            p.Id, p.IdCliente, p.IdMembresia, p.Monto, p.Metodo,
            p.IdRecepcionista, p.IdSucursal, p.Fecha, p.Notas,
            cliente = p.Cliente != null ? p.Cliente.Nombre : null,
            membresia = p.Membresia != null ? p.Membresia.Nombre : null,
            recepcionista = p.Recepcionista != null ? p.Recepcionista.Nombre : null,
            sucursal = p.Sucursal != null ? p.Sucursal.Nombre : null
        }).OrderByDescending(p => p.Fecha).ToListAsync() });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Pago item)
    {
        var cliente = await _db.Clientes.FindAsync(item.IdCliente);
        var membresia = await _db.Membresias.FindAsync(item.IdMembresia);
        if (cliente == null || membresia == null)
            return BadRequest(new { error = "Cliente o membresia no encontrado" });

        _db.Pagos.Add(item);

        // El pago SUMA dias sobre lo que le queda (si ya vencio, empieza desde hoy).
        var hoy = DateTime.Today;
        var baseFecha = (cliente.FechaVigencia.HasValue && cliente.FechaVigencia.Value > hoy)
            ? cliente.FechaVigencia.Value
            : hoy;
        cliente.FechaVigencia = baseFecha.AddDays(membresia.DuracionDias);

        await _db.SaveChangesAsync();
        return Ok(item);
    }

    public class PagoClienteRequest { public int IdMembresia { get; set; } }

    // Pago en linea desde el portal del cliente (simulado). Suma dias a su vigencia.
    [Authorize(Roles = "cliente")]
    [HttpPost("cliente")]
    public async Task<IActionResult> CreateCliente([FromBody] PagoClienteRequest req)
    {
        var idCli = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
        var cliente = await _db.Clientes.FindAsync(idCli);
        var membresia = await _db.Membresias.FindAsync(req.IdMembresia);
        if (cliente == null || membresia == null) return BadRequest(new { error = "Datos invalidos" });

        var procesador = await _db.Usuarios.Where(u => u.Rol == "admin_general").Select(u => u.Id).FirstOrDefaultAsync();
        _db.Pagos.Add(new Pago
        {
            IdCliente = cliente.Id, IdMembresia = membresia.Id, Monto = membresia.Precio,
            Metodo = "tarjeta", IdRecepcionista = procesador, IdSucursal = cliente.IdSucursal,
            Fecha = DateTime.Today, Notas = "Pago en linea (portal cliente)"
        });

        var hoy = DateTime.Today;
        var baseFecha = (cliente.FechaVigencia.HasValue && cliente.FechaVigencia.Value > hoy) ? cliente.FechaVigencia.Value : hoy;
        cliente.FechaVigencia = baseFecha.AddDays(membresia.DuracionDias);

        await _db.SaveChangesAsync();
        return Ok(new { vigencia = cliente.FechaVigencia, membresia = membresia.Nombre });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Pagos.FindAsync(id);
        if (item == null) return NotFound();
        _db.Pagos.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
