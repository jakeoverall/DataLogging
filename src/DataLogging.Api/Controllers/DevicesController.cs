using DataLogging.Api.Infrastructure;
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

    public DevicesController(
        DeviceRegistrationService deviceRegistrationService,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistrationService);
        ArgumentNullException.ThrowIfNull(services);

        _deviceRegistrationService = deviceRegistrationService;
        _services = services;
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

    private static DeviceSummaryResponse ToSummary(
        DeviceDefinition definition,
        DataLogging.MockHardware.Models.MockDeviceStateSnapshot? state,
        DateTimeOffset now)
    {
        var isOnline = definition.Enabled && (state?.IsOnline ?? false);
        var hasDrops = (state?.MessagesDropped ?? 0) > 0;
        var status = !definition.Enabled
            ? "offline"
            : isOnline
                ? (hasDrops ? "warning" : "online")
                : "offline";

        var health = CalculateHealth(definition.Enabled, state);

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
            LastSeen = FormatLastSeen(state?.LastEmissionTimestamp, now),
            LastValue = state is { SequenceNumber: > 0 }
                ? $"Sequence {state.SequenceNumber}"
                : "No recent value",
            AlertCount = status switch
            {
                "offline" => 1,
                "warning" => 1,
                _ => 0
            }
        };
    }

    private static int CalculateHealth(
        bool enabled,
        DataLogging.MockHardware.Models.MockDeviceStateSnapshot? state)
    {
        if (!enabled)
        {
            return 0;
        }

        if (state is null)
        {
            return 70;
        }

        if (!state.IsOnline)
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
