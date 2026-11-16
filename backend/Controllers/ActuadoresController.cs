using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FitConnectAPI.Services;

namespace FitConnectAPI.Controllers;

// Control remoto de los actuadores del kiosko (LED verde, LED rojo, buzzer)
// desde el dashboard web. El backend publica el comando por MQTT y el ESP32,
// suscrito a fitconnect/<idSucursal>/actuador, ejecuta la accion en el hardware.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ActuadoresController : ControllerBase
{
    private readonly MqttService _mqtt;
    public ActuadoresController(MqttService mqtt) => _mqtt = mqtt;

    public class ComandoRequest
    {
        public int IdSucursal { get; set; }
        public string Actuador { get; set; } = "";   // led_verde | led_rojo | buzzer
        public string Estado { get; set; } = "";      // on | off | pulso
    }

    [HttpPost("comando")]
    public async Task<IActionResult> Comando([FromBody] ComandoRequest req)
    {
        // El admin de sede / recepcionista solo puede mandar a SU sucursal.
        var rol = User.FindFirst("rol")?.Value;
        var idSuc = User.FindFirst("idSucursal")?.Value;
        var idSucursal = (rol != "admin_general" && idSuc != null) ? int.Parse(idSuc) : req.IdSucursal;

        var payload = JsonSerializer.Serialize(new
        {
            accion = "comando",
            actuador = req.Actuador,
            estado = req.Estado
        });

        var ok = await _mqtt.PublishAsync($"fitconnect/{idSucursal}/actuador", payload);
        if (!ok) return StatusCode(503, new { error = "Broker MQTT no disponible" });
        return Ok(new { enviado = true, idSucursal, req.Actuador, req.Estado });
    }
}
