using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AsistenciasController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public AsistenciasController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.Asistencias
            .Include(a => a.Cliente)
            .Include(a => a.Sucursal)
            .AsQueryable();

        if (rol != "admin_general" && idSuc != null)
            query = query.Where(a => a.IdSucursal == int.Parse(idSuc));

        if (rol == "cliente")
        {
            var idCliente = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
            query = query.Where(a => a.IdCliente == idCliente);
        }

        return Ok(new { items = await query.Select(a => new
        {
            a.Id, a.IdCliente, a.IdSucursal, a.UidNfc, a.FechaHora, a.Metodo,
            cliente = a.Cliente != null ? a.Cliente.Nombre : null,
            sucursal = a.Sucursal != null ? a.Sucursal.Nombre : null
        }).OrderByDescending(a => a.FechaHora).ToListAsync() });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Asistencia item)
    {
        _db.Asistencias.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Asistencias.FindAsync(id);
        if (item == null) return NotFound();
        _db.Asistencias.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }

    public class AccesoRequest { public string Uid { get; set; } = ""; }

    // Control de acceso por NFC: busca al socio por UID en CUALQUIER sucursal y decide
    // si puede entrar a la sucursal de este kiosko. Si procede, registra la asistencia.
    [Authorize(Roles = "recepcionista,kiosko")]
    [HttpPost("acceso")]
    public async Task<IActionResult> Acceso([FromBody] AccesoRequest req)
    {
        var idSucStr = User.FindFirst("idSucursal")?.Value;
        int? kioskSuc = idSucStr != null ? int.Parse(idSucStr) : null;

        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.UidNfc == req.Uid);
        if (cliente == null) return Ok(new { estado = "no_registrado", uid = req.Uid });

        var ultimoPago = await _db.Pagos.Include(p => p.Membresia)
            .Where(p => p.IdCliente == cliente.Id)
            .OrderByDescending(p => p.Fecha).ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync();
        var membresia = ultimoPago?.Membresia?.Nombre;
        var dias = cliente.FechaVigencia.HasValue ? (cliente.FechaVigencia.Value.Date - DateTime.Today).Days : 0;
        var vigente = cliente.FechaVigencia.HasValue && cliente.FechaVigencia.Value >= DateTime.Today;

        object Info(string estado, bool vis = false) => new
        {
            estado, nombre = cliente.Nombre, membresia,
            vigencia = cliente.FechaVigencia, diasRestantes = dias, visitante = vis
        };

        if (!vigente) return Ok(Info("vencida"));

        var mismaSede = kioskSuc == null || cliente.IdSucursal == kioskSuc.Value;
        var visitante = false;
        if (!mismaSede)
        {
            // Solo entra a otra sede si su membresia actual lo permite (ej. Premium).
            if (!(ultimoPago?.Membresia?.AccesoTodasSedes ?? false))
                return Ok(Info("otra_sede"));
            visitante = true;
        }

        _db.Asistencias.Add(new Asistencia
        {
            IdCliente = cliente.Id,
            IdSucursal = kioskSuc ?? cliente.IdSucursal,
            UidNfc = req.Uid,
            Metodo = "nfc"
        });
        await _db.SaveChangesAsync();

        return Ok(Info("ok", visitante));
    }
}
