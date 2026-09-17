using DataLogging.Api.Infrastructure;
using DataLogging.Api.Ingestion;
using DataLogging.Api.Models;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Registry;
using Microsoft.AspNetCore.Mvc;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/devices")]
public sealed class DevicesController : ControllerBase
{
    private static readonly TimeSpan StreamUpdateInterval = TimeSpan.FromSeconds(1);

    private readonly DeviceRegistrationService _deviceRegistrationService;
    private readonly IServiceProvider _services;
    private readonly WebSocketConnectionTracker _webSocketConnectionTracker;

    public DevicesController(
        DeviceRegistrationService deviceRegistrationService,
        IServiceProvider services,
        WebSocketConnectionTracker webSocketConnectionTracker)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistrationService);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(webSocketConnectionTracker);

        _deviceRegistrationService = deviceRegistrationService;
        _services = services;
        _webSocketConnectionTracker = webSocketConnectionTracker;
    }

    [HttpGet]
    public IActionResult GetDevices()
    {
        return Ok(BuildDeviceSummaries());
    }

    [HttpPut("{deviceId}")]
    public async Task<IActionResult> UpsertDeviceAsync(
        string deviceId,
        [FromBody] UpsertDeviceRequest request,
        CancellationToken cancellationToken)
    {
        var device = new DeviceDefinition
        {
            DeviceId = deviceId,
            Name = request.Name,
            DeviceType = request.DeviceType,
            Protocol = request.Protocol,
            Address = request.Address,
            Port = request.Port,
            Enabled = request.Enabled,
            Properties = request.Properties ?? new Dictionary<string, string>()
        };

        await _deviceRegistrationService.UpsertAsync(device, cancellationToken).ConfigureAwait(false);
        return NoContent();
    }

    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        var removed = await _deviceRegistrationService.RemoveAsync(deviceId, cancellationToken).ConfigureAwait(false);
        return removed ? NoContent() : NotFound();
    }

    [HttpGet("stream")]
    public async Task StreamDevicesAsync(CancellationToken cancellationToken)
    {
        SseResponseWriter.Configure(Response);

        try
        {
            await SseResponseWriter.WriteEventAsync(Response, "connected", new { status = "connected" }, cancellationToken).ConfigureAwait(false);
            await SseResponseWriter.WriteEventAsync(
                Response,
                "snapshot",
                new DeviceStreamSnapshotResponse
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Devices = BuildDeviceSummaries()
                },
                cancellationToken).ConfigureAwait(false);

            using var timer = new PeriodicTimer(StreamUpdateInterval);
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var payload = new DeviceStreamSnapshotResponse
                {
                    Timestamp = DateTimeOffset.UtcNow,
                    Devices = BuildDeviceSummaries()
                };

                await SseResponseWriter.WriteEventAsync(Response, "snapshot", payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client disconnected or the request was aborted; treat this as a normal shutdown.
        }
    }

    private IReadOnlyCollection<DeviceSummaryResponse> BuildDeviceSummaries()
    {
        var stateById = _services.GetService<MockDeviceRegistry>()?
            .GetDeviceStates()
            .ToDictionary(state => state.DeviceId, StringComparer.Ordinal)
            ?? new Dictionary<string, DataLogging.MockHardware.Models.MockDeviceStateSnapshot>(StringComparer.Ordinal);

        return _deviceRegistrationService.GetAll()
            .OrderBy(device => device.DeviceId, StringComparer.Ordinal)
            .Select(device =>
            {
                stateById.TryGetValue(device.DeviceId, out var state);
                return ToSummary(device, state, DateTimeOffset.UtcNow);
            })
            .ToArray();
    }

    private DeviceSummaryResponse ToSummary(
        DeviceDefinition definition,
        DataLogging.MockHardware.Models.MockDeviceStateSnapshot? state,
        DateTimeOffset now)
    {
        var websocketIsOnline = definition.Protocol == DataLogging.Ingestion.Models.DeviceProtocol.WebSocket
            && definition.Enabled
            && _webSocketConnectionTracker.IsConnected(definition.DeviceId);
        var isOnline = definition.Enabled && (state?.IsOnline ?? websocketIsOnline);
        var hasDrops = (state?.MessagesDropped ?? 0) > 0;
        var status = !definition.Enabled
            ? "offline"
            : isOnline
                ? (hasDrops ? "warning" : "online")
                : "offline";

        var health = CalculateHealth(definition.Enabled, state, websocketIsOnline);
        var alertCount = !definition.Enabled
            ? 0
            : status switch
            {
                "offline" => 1,
                "warning" => 1,
                _ => 0
            };

        return new DeviceSummaryResponse
        {
            Id = definition.DeviceId,
            DeviceId = definition.DeviceId,
            Name = definition.Name,
            DeviceType = definition.DeviceType,
            Protocol = definition.Protocol,
            Status = status,
            Health = health,
            Address = string.IsNullOrWhiteSpace(definition.Address) ? "127.0.0.1" : definition.Address,
            Port = ParsePort(definition.Port),
            Enabled = definition.Enabled,
            LastSeen = definition.Enabled
                ? FormatLastSeen(state?.LastEmissionTimestamp, now)
                : "Disabled",
            LastValue = !definition.Enabled
                ? "Device disabled"
                : state is { SequenceNumber: > 0 }
                ? $"Sequence {state.SequenceNumber}"
                : "No recent value",
            AlertCount = alertCount
        };
    }

    private static int CalculateHealth(
        bool enabled,
        DataLogging.MockHardware.Models.MockDeviceStateSnapshot? state,
        bool websocketIsOnline = false)
    {
        if (!enabled)
        {
            return 0;
        }

        if (state is null)
        {
            return websocketIsOnline ? 100 : 70;
        }

        if (!state.IsOnline && !websocketIsOnline)
        {
            return 25;
        }

        var total = state.MessagesEmitted + state.MessagesDropped;
        if (total <= 0)
        {
            return 100;
        }

        var dropRatio = (double)state.MessagesDropped / total;
        var health = (int)Math.Round(100 - (dropRatio * 100), MidpointRounding.AwayFromZero);
        return Math.Clamp(health, 0, 100);
    }

    private static int ParsePort(string? value)
    {
        if (int.TryParse(value, out var port))
        {
            return port;
        }

        return 9000;
    }

    private static string FormatLastSeen(DateTimeOffset? timestamp, DateTimeOffset now)
    {
        if (timestamp is null)
        {
            return "No telemetry yet";
        }

        var age = now - timestamp.Value;
        if (age <= TimeSpan.FromSeconds(30))
        {
            return "Just now";
        }

        if (age <= TimeSpan.FromMinutes(1))
        {
            return $"{Math.Max(1, (int)Math.Round(age.TotalSeconds, MidpointRounding.AwayFromZero))} sec ago";
        }

        if (age <= TimeSpan.FromHours(1))
        {
            return $"{Math.Max(1, (int)Math.Round(age.TotalMinutes, MidpointRounding.AwayFromZero))} min ago";
        }

        return $"{Math.Max(1, (int)Math.Round(age.TotalHours, MidpointRounding.AwayFromZero))} hr ago";
    }
}
