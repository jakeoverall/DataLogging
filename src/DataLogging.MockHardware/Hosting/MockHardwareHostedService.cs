using DataLogging.MockHardware.Simulation;
using Microsoft.Extensions.Hosting;

namespace DataLogging.MockHardware.Hosting;

public sealed class MockHardwareHostedService : BackgroundService
{
    private readonly MockHardwareSimulation _simulation;
    private readonly IHostApplicationLifetime _lifetime;

    public MockHardwareHostedService(
        MockHardwareSimulation simulation,
        IHostApplicationLifetime lifetime)
    {
        ArgumentNullException.ThrowIfNull(simulation);
        ArgumentNullException.ThrowIfNull(lifetime);

        _simulation = simulation;
        _lifetime = lifetime;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return RunAndStopAsync(stoppingToken);
    }

    private async Task RunAndStopAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _simulation.RunAsync(stoppingToken).ConfigureAwait(false);
        }
        finally
        {
            _lifetime.StopApplication();
        }
    }
}
