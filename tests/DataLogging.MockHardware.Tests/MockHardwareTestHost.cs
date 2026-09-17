using DataLogging.MockHardware.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DataLogging.MockHardware.Tests;

internal static class MockHardwareTestHost
{
    public static IHost CreateHost(Action<IDictionary<string, string?>>? configure = null)
    {
        var builder = Host.CreateApplicationBuilder();
        var settings = new Dictionary<string, string?>
        {
            ["NDevices"] = "3",
            ["EmitFrequency"] = "50ms",
            ["BufferCapacity"] = "8"
        };

        configure?.Invoke(settings);
        builder.Configuration.AddInMemoryCollection(settings);
        builder.Services.AddMockHardware(builder.Configuration);

        return builder.Build();
    }
}
