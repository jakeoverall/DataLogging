using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Hosting;
using DataLogging.MockHardware.Registry;
using DataLogging.MockHardware.Simulation;
using DataLogging.MockHardware.Sources;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Configuration;

public static class MockHardwareServiceCollectionExtensions
{
    public static IServiceCollection AddMockHardware(
        this IServiceCollection services,
        IConfiguration configuration,
        bool stopHostOnCompletion = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IConfigureOptions<MockHardwareOptions>>(new MockHardwareOptionsSetup(configuration));
        services.AddSingleton<IValidateOptions<MockHardwareOptions>, MockHardwareOptionsValidator>();
        services.AddOptions<MockHardwareOptions>().ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MockDeviceRegistry>();
        services.AddSingleton<IMockHardwareController, MockHardwareController>();
        services.AddSingleton<MockRos2Source>();
        services.AddSingleton<IMockRos2Source>(sp => sp.GetRequiredService<MockRos2Source>());
        services.AddSingleton<MockCanOpenSource>();
        services.AddSingleton<IMockCanOpenSource>(sp => sp.GetRequiredService<MockCanOpenSource>());
        services.AddSingleton<MockEthernetSource>();
        services.AddSingleton<IMockEthernetSource>(sp => sp.GetRequiredService<MockEthernetSource>());
        services.AddSingleton<MockHardwareSimulation>();
        if (stopHostOnCompletion)
        {
            services.AddHostedService<MockHardwareHostedService>();
        }
        else
        {
            services.AddHostedService<MockHardwareContinuousHostedService>();
        }

        return services;
    }
}
