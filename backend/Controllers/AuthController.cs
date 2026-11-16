using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly FitConnectDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(FitConnectDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email == req.Email && u.Password == req.Password && u.Estatus == "activo");
        if (user == null) return Unauthorized(new { error = "Credenciales incorrectas" });

        var token = GenerateToken(user.Id.ToString(), user.Rol, user.IdSucursal?.ToString());
        return Ok(new LoginResponse
        {
            Token = token,
            Rol = user.Rol,
            Nombre = user.Nombre,
            IdSucursal = user.IdSucursal,
            Id = user.Id
        });
    }

    [HttpPost("login-cliente")]
    public async Task<IActionResult> LoginCliente([FromBody] LoginClienteRequest req)
    {
        var cliente = await _db.Clientes.FirstOrDefaultAsync(c => c.Email == req.Email && c.Password == req.Password && c.Estatus == "activo");
        if (cliente == null) return Unauthorized(new { error = "Credenciales incorrectas" });

        var ultimoPago = await _db.Pagos
            .Include(p => p.Membresia)
            .Where(p => p.IdCliente == cliente.Id)
            .OrderByDescending(p => p.Fecha)
            .FirstOrDefaultAsync();

        var sucursalNombre = (await _db.Sucursales.FindAsync(cliente.IdSucursal))?.Nombre;
        var token = GenerateToken(cliente.Id.ToString(), "cliente", cliente.IdSucursal.ToString());
        return Ok(new
        {
            token,
            rol = "cliente",
            nombre = cliente.Nombre,
            id = cliente.Id,
            email = cliente.Email,
            telefono = cliente.Telefono,
            idSucursal = cliente.IdSucursal,
            sucursal = sucursalNombre,
            uidNfc = cliente.UidNfc,
            vigencia = cliente.FechaVigencia,
            membresia = ultimoPago?.Membresia?.Nombre
        });
    }

    [HttpPost("cambio-password-cliente")]
    public async Task<IActionResult> CambioPasswordCliente([FromBody] CambioPasswordRequest req)
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (idClaim == null) return Unauthorized();
        var cliente = await _db.Clientes.FindAsync(int.Parse(idClaim));
        if (cliente == null) return NotFound();
        if (cliente.Password != req.PasswordActual) return BadRequest(new { error = "Contraseña actual incorrecta" });

        cliente.Password = req.PasswordNueva;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Contraseña actualizada" });
    }

    private string GenerateToken(string id, string rol, string? idSucursal)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"] ?? "FitConnectPro2026SuperSecretKey!@#$%^&*()"));
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, id),
            new("rol", rol)
        };
        if (idSucursal != null) claims.Add(new Claim("idSucursal", idSucursal));

        // El kiosco de entrada corre 24/7 con una sesion fija: token de larga duracion.
        var expires = rol == "kiosko" ? DateTime.UtcNow.AddYears(5) : DateTime.UtcNow.AddHours(12);
        var token = new JwtSecurityToken(
            claims: claims,
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
