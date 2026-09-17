using DataLogging.Core.Abstractions;
using DataLogging.Ingestion;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Registry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataLogging.Api.Ingestion;

public sealed class WebSocketDeviceBridgeHostedService : BackgroundService
{
    private readonly DeviceRegistrationService _deviceRegistrationService;
    private readonly IngestionService _ingestionService;
    private readonly WebSocketConnectionTracker _connectionTracker;
    private readonly ILogSerializer _serializer;
    private readonly ILogWriter _writer;
    private readonly ILogger<WebSocketDeviceBridgeHostedService> _logger;

    public WebSocketDeviceBridgeHostedService(
        DeviceRegistrationService deviceRegistrationService,
        IngestionService ingestionService,
        WebSocketConnectionTracker connectionTracker,
        ILogSerializer serializer,
        ILogWriter writer,
        ILogger<WebSocketDeviceBridgeHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistrationService);
        ArgumentNullException.ThrowIfNull(ingestionService);
        ArgumentNullException.ThrowIfNull(connectionTracker);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(logger);

        _deviceRegistrationService = deviceRegistrationService;
        _ingestionService = ingestionService;
        _connectionTracker = connectionTracker;
        _serializer = serializer;
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting WebSocket device bridge.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var devices = _deviceRegistrationService.GetAll()
                .Where(device => device.Protocol == DataLogging.Ingestion.Models.DeviceProtocol.WebSocket && device.Enabled)
                .ToArray();

            foreach (var device in devices)
            {
                if (!await TryBridgeDeviceAsync(device, stoppingToken).ConfigureAwait(false))
                {
                    _logger.LogWarning("Could not establish WebSocket connection for device '{DeviceId}' at {Address}:{Port}", device.DeviceId, device.Address, device.Port);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> TryBridgeDeviceAsync(
        DataLogging.Ingestion.Models.DeviceDefinition device,
        CancellationToken cancellationToken)
    {
        await using var source = new WebSocketDeviceDataSource(device, _connectionTracker);

        try
        {
            await foreach (var record in _ingestionService.IngestAsync(source, cancellationToken).WithCancellation(cancellationToken))
            {
                var envelope = _serializer.Serialize(record);
                await _writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket bridge failed for device '{DeviceId}'.", device.DeviceId);
            return false;
        }
    }
}
