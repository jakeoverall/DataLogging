using DataLogging.MockHardware.Simulation;
using Microsoft.Extensions.Hosting;

namespace DataLogging.MockHardware.Hosting;

public sealed class MockHardwareContinuousHostedService : BackgroundService
{
    private readonly MockHardwareSimulation _simulation;

    public MockHardwareContinuousHostedService(MockHardwareSimulation simulation)
    {
        ArgumentNullException.ThrowIfNull(simulation);

        _simulation = simulation;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _simulation.RunAsync(stoppingToken);
    }
}
