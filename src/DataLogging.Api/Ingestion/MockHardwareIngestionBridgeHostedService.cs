using DataLogging.Core.Abstractions;
using DataLogging.Ingestion;
using DataLogging.Ingestion.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataLogging.Api.Ingestion;

public sealed class MockHardwareIngestionBridgeHostedService : BackgroundService
{
    private readonly IngestionService _ingestionService;
    private readonly MockRos2IngestionDataSource _ros2Source;
    private readonly MockCanOpenIngestionDataSource _canOpenSource;
    private readonly MockEthernetIngestionDataSource _ethernetSource;
    private readonly ILogSerializer _serializer;
    private readonly ILogWriter _writer;
    private readonly ILogger<MockHardwareIngestionBridgeHostedService> _logger;

    public MockHardwareIngestionBridgeHostedService(
        IngestionService ingestionService,
        MockRos2IngestionDataSource ros2Source,
        MockCanOpenIngestionDataSource canOpenSource,
        MockEthernetIngestionDataSource ethernetSource,
        ILogSerializer serializer,
        ILogWriter writer,
        ILogger<MockHardwareIngestionBridgeHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(ingestionService);
        ArgumentNullException.ThrowIfNull(ros2Source);
        ArgumentNullException.ThrowIfNull(canOpenSource);
        ArgumentNullException.ThrowIfNull(ethernetSource);
        ArgumentNullException.ThrowIfNull(serializer);
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(logger);

        _ingestionService = ingestionService;
        _ros2Source = ros2Source;
        _canOpenSource = canOpenSource;
        _ethernetSource = ethernetSource;
        _serializer = serializer;
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting mock hardware ingestion bridge.");

        var tasks = new[]
        {
            BridgeSourceAsync(_ros2Source, stoppingToken),
            BridgeSourceAsync(_canOpenSource, stoppingToken),
            BridgeSourceAsync(_ethernetSource, stoppingToken)
        };

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task BridgeSourceAsync(IDataSource source, CancellationToken cancellationToken)
    {
        await foreach (var record in _ingestionService.IngestAsync(source, cancellationToken).WithCancellation(cancellationToken))
        {
            var envelope = _serializer.Serialize(record);
            await _writer.WriteAsync(envelope, cancellationToken).ConfigureAwait(false);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }
}
