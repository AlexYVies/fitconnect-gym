using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FitConnectAPI.Data;
using FitConnectAPI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<FitConnectDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? "FitConnectPro2026SuperSecretKey!@#$%^&*()")),
            ValidateIssuer = false,
            ValidateAudience = false,
            RoleClaimType = "rol",
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

builder.Services.AddControllers();

// Servicio MQTT: Singleton (para publicar desde los controllers).
// El HostedService (conexion + suscripcion a las lecturas NFC del ESP32) solo
// arranca si hay broker configurado (Mqtt:Host). Asi, en IIS sin broker no hace
// ruido: el ActuadoresController responde 503 si no hay conexion.
builder.Services.AddSingleton<MqttService>();
if (!string.IsNullOrWhiteSpace(builder.Configuration["Mqtt:Host"]))
    builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttService>());

var app = builder.Build();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();

app.Run();
