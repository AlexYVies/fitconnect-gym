using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Client;
using FitConnectAPI.Data;
using FitConnectAPI.Models;

namespace FitConnectAPI.Services;

// Servicio de comunicacion MQTT (real, no simulada) entre el ESP32 y el backend.
//
//   - El ESP32 PUBLICA las lecturas NFC en:      fitconnect/<idSucursal>/nfc
//     payload: {"uid":"AA BB CC DD"}
//   - El backend se SUSCRIBE, persiste la lectura y (si el llavero ya esta
//     asignado a un cliente) devuelve el resultado de acceso PUBLICANDO en:
//     fitconnect/<idSucursal>/actuador
//     payload: {"accion":"acceso","estado":"ok|denegado"}   -> LED/buzzer
//   - El dashboard puede mandar comandos manuales a los actuadores llamando a
//     /api/actuadores/comando, que a su vez publica en el mismo topic.
//
// Se registra como Singleton (para que el ActuadoresController pueda publicar)
// y como HostedService (para mantener viva la conexion/suscripcion).
public class MqttService : IHostedService, IDisposable
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<MqttService> _log;
    private IMqttClient? _client;
    private MqttClientOptions? _options;
    private CancellationTokenSource? _cts;

    public MqttService(IServiceProvider services, IConfiguration config, ILogger<MqttService> log)
    {
        _services = services;
        _config = config;
        _log = log;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token); // arranca en segundo plano; no bloquea el startup
        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken token)
    {
        var host = _config["Mqtt:Host"] ?? "localhost";
        var port = int.TryParse(_config["Mqtt:Port"], out var p) ? p : 1883;

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(host, port)
            .WithClientId("fitconnect-backend")
            .WithCleanSession()
            .Build();

        _client.ApplicationMessageReceivedAsync += OnMessageAsync;

        // Bucle de conexion con reintentos: sobrevive a que el broker aun no este listo.
        while (!token.IsCancellationRequested)
        {
            try
            {
                if (!_client.IsConnected)
                {
                    await _client.ConnectAsync(_options, token);
                    await _client.SubscribeAsync(
                        new MqttTopicFilterBuilder().WithTopic("fitconnect/+/nfc").Build(), token);
                    await _client.SubscribeAsync(
                        new MqttTopicFilterBuilder().WithTopic("fitconnect/+/sensor").Build(), token);
                    _log.LogInformation("MQTT conectado a {Host}:{Port}, suscrito a fitconnect/+/nfc y +/sensor", host, port);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning("MQTT sin conexion ({Msg}). Reintentando en 5s...", ex.Message);
            }
            await Task.Delay(TimeSpan.FromSeconds(5), token);
        }
    }

    private async Task OnMessageAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        try
        {
            var topic = e.ApplicationMessage.Topic;                       // fitconnect/<idSucursal>/<tipo>
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);
            var parts = topic.Split('/');
            if (parts.Length < 3) return;
            if (!int.TryParse(parts[1], out var idSucursal)) return;
            var canal = parts[2];   // nfc | sensor

            var doc = JsonDocument.Parse(payload);

            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FitConnectDbContext>();

            // === Sensores fisicos (ultrasonico, boton): solo persistir la lectura ===
            if (canal == "sensor")
            {
                var tipo = doc.RootElement.TryGetProperty("tipo", out var tEl) ? tEl.GetString() ?? "desconocido" : "desconocido";
                double valor = doc.RootElement.TryGetProperty("valor", out var vEl) && vEl.TryGetDouble(out var vv) ? vv : 0;
                db.LecturasSensor.Add(new LecturaSensor { Tipo = tipo, Valor = valor, IdSucursal = idSucursal, Timestamp = DateTime.Now });
                await db.SaveChangesAsync();
                _log.LogInformation("MQTT SENSOR sucursal={Suc} tipo={Tipo} valor={Valor}", idSucursal, tipo, valor);
                return;
            }

            // === NFC ===
            if (!doc.RootElement.TryGetProperty("uid", out var uidEl)) return;
            var uid = uidEl.GetString();
            if (string.IsNullOrWhiteSpace(uid)) return;

            // 1) Guardar / actualizar la lectura (mismo comportamiento que el endpoint HTTP por-uid)
            var lectura = await db.NfcLecturas.FirstOrDefaultAsync(n => n.Uid == uid);
            if (lectura != null)
            {
                lectura.Timestamp = DateTime.Now;
                lectura.IdSucursal = idSucursal;
            }
            else
            {
                db.NfcLecturas.Add(new NfcLectura { Uid = uid, IdSucursal = idSucursal, Timestamp = DateTime.Now });
            }
            await db.SaveChangesAsync();

            // 2) Resolver acceso y responder al actuador (LED verde/rojo del kiosko)
            var cliente = await db.Clientes.FirstOrDefaultAsync(c => c.UidNfc == uid);
            string estado;
            if (cliente == null)
            {
                estado = "denegado"; // llavero no asignado
            }
            else
            {
                var vigente = cliente.FechaVigencia.HasValue && cliente.FechaVigencia.Value >= DateTime.Today;
                estado = vigente ? "ok" : "denegado";
            }
            await PublishAsync($"fitconnect/{idSucursal}/actuador",
                JsonSerializer.Serialize(new { accion = "acceso", estado }));

            _log.LogInformation("MQTT NFC sucursal={Suc} uid={Uid} -> {Estado}", idSucursal, uid, estado);
        }
        catch (Exception ex)
        {
            _log.LogError("Error procesando mensaje MQTT: {Msg}", ex.Message);
        }
    }

    // Usado por el ActuadoresController para mandar comandos manuales desde el dashboard.
    public async Task<bool> PublishAsync(string topic, string payload)
    {
        if (_client == null || !_client.IsConnected) return false;
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();
        await _client.PublishAsync(msg);
        return true;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _cts?.Cancel();
        if (_client != null && _client.IsConnected)
            await _client.DisconnectAsync();
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _client?.Dispose();
    }
}
