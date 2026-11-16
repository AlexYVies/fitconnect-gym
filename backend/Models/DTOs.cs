namespace FitConnectAPI.Models;

public class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginClienteRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

public class LoginResponse
{
    public string Token { get; set; } = "";
    public string Rol { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int? IdSucursal { get; set; }
    public int Id { get; set; }
}

public class CambioPasswordRequest
{
    public string PasswordActual { get; set; } = "";
    public string PasswordNueva { get; set; } = "";
}
