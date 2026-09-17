using DataLogging.Ingestion.Registry;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DataLogging.Ingestion.Hosting;

public sealed class DeviceRegistrationInitializationHostedService : IHostedService
{
    private readonly DeviceRegistrationService _registrationService;
    private readonly ILogger<DeviceRegistrationInitializationHostedService> _logger;

    public DeviceRegistrationInitializationHostedService(
        DeviceRegistrationService registrationService,
        ILogger<DeviceRegistrationInitializationHostedService> logger)
    {
        ArgumentNullException.ThrowIfNull(registrationService);
        ArgumentNullException.ThrowIfNull(logger);

        _registrationService = registrationService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _registrationService.InitializeAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Initialized ingestion device registry.");
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
