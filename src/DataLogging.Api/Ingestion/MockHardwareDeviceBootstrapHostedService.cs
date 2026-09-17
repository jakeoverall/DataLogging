using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Models;
using DataLogging.MockHardware.Registry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataLogging.Api.Ingestion;

public sealed class MockHardwareDeviceBootstrapHostedService : BackgroundService
{
    private readonly MockDeviceRegistry _mockRegistry;
    private readonly DeviceRegistrationService _registrationService;
    private readonly ILogger<MockHardwareDeviceBootstrapHostedService> _logger;

    public MockHardwareDeviceBootstrapHostedService(
        MockDeviceRegistry mockRegistry,
        DeviceRegistrationService registrationService,
        ILogger<MockHardwareDeviceBootstrapHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(mockRegistry);
        ArgumentNullException.ThrowIfNull(registrationService);
        ArgumentNullException.ThrowIfNull(logger);

        _mockRegistry = mockRegistry;
        _registrationService = registrationService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var mockDevices = _mockRegistry.GetDevices();
            foreach (var mockDevice in mockDevices)
            {
                var definition = ToDeviceDefinition(mockDevice);
                await _registrationService.UpsertAsync(definition, stoppingToken).ConfigureAwait(false);
            }

            if (mockDevices.Count > 0)
            {
                _logger.LogInformation("Bootstrapped ingestion registry from {Count} mock device descriptors.", mockDevices.Count);
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), stoppingToken).ConfigureAwait(false);
        }
    }

    private static DataLogging.Ingestion.Models.DeviceDefinition ToDeviceDefinition(MockDeviceDescriptor descriptor)
    {
        return new DataLogging.Ingestion.Models.DeviceDefinition
        {
            DeviceId = descriptor.DeviceId,
            Name = descriptor.Name,
            DeviceType = descriptor.DeviceType,
            Protocol = descriptor.Protocol switch
            {
                DataLogging.MockHardware.Models.DeviceProtocol.Ros2 => DataLogging.Ingestion.Models.DeviceProtocol.Ros2,
                DataLogging.MockHardware.Models.DeviceProtocol.CanOpen => DataLogging.Ingestion.Models.DeviceProtocol.CanOpen,
                DataLogging.MockHardware.Models.DeviceProtocol.Ethernet => DataLogging.Ingestion.Models.DeviceProtocol.Ethernet,
                _ => throw new InvalidOperationException($"Unsupported mock protocol '{descriptor.Protocol}'.")
            },
            Address = descriptor.Address,
            Port = null,
            Enabled = true,
            Properties = BuildProperties(descriptor)
        };
    }

    private static IReadOnlyDictionary<string, string> BuildProperties(MockDeviceDescriptor descriptor)
    {
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(descriptor.Topic))
        {
            properties["topic"] = descriptor.Topic;
        }

        if (descriptor.NodeId is { } nodeId)
        {
            properties["nodeId"] = nodeId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!string.IsNullOrWhiteSpace(descriptor.Address))
        {
            properties["address"] = descriptor.Address;
        }

        return properties;
    }
}
