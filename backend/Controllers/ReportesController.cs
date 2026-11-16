using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FitConnectAPI.Data;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ReportesController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    public ReportesController(FitConnectDbContext db) => _db = db;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        int? sucId = (rol != "admin_general" && idSuc != null) ? int.Parse(idSuc) : null;

        var hoy = DateTime.Today;
        var inicioMes = new DateTime(hoy.Year, hoy.Month, 1);

        var clientesQuery = _db.Clientes.Where(c => c.Estatus == "activo");
        var pagosQuery = _db.Pagos.AsQueryable();
        var asistenciasQuery = _db.Asistencias.AsQueryable();
        var ventasQuery = _db.Ventas.AsQueryable();

        if (sucId != null)
        {
            clientesQuery = clientesQuery.Where(c => c.IdSucursal == sucId);
            pagosQuery = pagosQuery.Where(p => p.IdSucursal == sucId);
            asistenciasQuery = asistenciasQuery.Where(a => a.IdSucursal == sucId);
            ventasQuery = ventasQuery.Where(v => v.IdSucursal == sucId);
        }

        return Ok(new
        {
            totalClientes = await clientesQuery.CountAsync(),
            asistenciasHoy = await asistenciasQuery.CountAsync(a => a.FechaHora.Date == hoy),
            ingresosMes = await pagosQuery.Where(p => p.Fecha >= inicioMes).SumAsync(p => p.Monto),
            ventasMes = await ventasQuery.Where(v => v.Fecha >= inicioMes).SumAsync(v => v.Total)
        });
    }
}
