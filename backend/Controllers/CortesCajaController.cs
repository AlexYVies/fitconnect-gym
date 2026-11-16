using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CortesCajaController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public CortesCajaController(FitConnectDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;

        var query = _db.CortesCaja
            .Include(c => c.Recepcionista)
            .Include(c => c.Sucursal)
            .AsQueryable();

        if (rol != "admin_general" && idSuc != null)
            query = query.Where(c => c.IdSucursal == int.Parse(idSuc));

        return Ok(new { items = await query.Select(c => new
        {
            c.Id, c.IdRecepcionista, c.IdSucursal,
            c.FechaApertura, c.FechaCierre,
            c.TotalMembresias, c.TotalProductos,
            c.TotalEfectivo, c.TotalTarjeta, c.TotalGeneral,
            c.Estatus,
            recepcionista = c.Recepcionista != null ? c.Recepcionista.Nombre : null,
            sucursal = c.Sucursal != null ? c.Sucursal.Nombre : null
        }).OrderByDescending(c => c.FechaApertura).ToListAsync() });
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CorteCaja item)
    {
        item.FechaApertura = DateTime.Now;
        item.Estatus = "abierto";
        _db.CortesCaja.Add(item);
        await _db.SaveChangesAsync();
        return Ok(item);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpPut("{id}/cerrar")]
    public async Task<IActionResult> Cerrar(int id)
    {
        var corte = await _db.CortesCaja.FindAsync(id);
        if (corte == null) return NotFound();

        var pagos = await _db.Pagos
            .Where(p => p.IdSucursal == corte.IdSucursal && p.Fecha >= corte.FechaApertura.Date && p.Fecha <= DateTime.Today)
            .ToListAsync();

        var ventas = await _db.Ventas
            .Where(v => v.IdSucursal == corte.IdSucursal && v.Fecha >= corte.FechaApertura.Date && v.Fecha <= DateTime.Today)
            .ToListAsync();

        corte.TotalMembresias = pagos.Sum(p => p.Monto);
        corte.TotalProductos = ventas.Sum(v => v.Total);
        corte.TotalEfectivo = pagos.Where(p => p.Metodo == "efectivo").Sum(p => p.Monto) + ventas.Where(v => v.Metodo == "efectivo").Sum(v => v.Total);
        corte.TotalTarjeta = pagos.Where(p => p.Metodo == "tarjeta").Sum(p => p.Monto) + ventas.Where(v => v.Metodo == "tarjeta").Sum(v => v.Total);
        corte.TotalGeneral = corte.TotalMembresias + corte.TotalProductos;
        corte.FechaCierre = DateTime.Now;
        corte.Estatus = "cerrado";

        await _db.SaveChangesAsync();
        return Ok(corte);
    }

    [Authorize(Roles = "recepcionista")]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.CortesCaja.FindAsync(id);
        if (item == null) return NotFound();
        _db.CortesCaja.Remove(item);
        await _db.SaveChangesAsync();
        return Ok();
    }
}
