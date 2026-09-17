using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Hosting;
using DataLogging.Ingestion.Normalization;
using DataLogging.Ingestion.Registry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataLogging.Ingestion;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLoggingIngestion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<DeviceRegistrationOptions>, DeviceRegistrationOptionsValidator>();
        services.AddOptions<DeviceRegistrationOptions>().ValidateOnStart();

        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<InMemoryDeviceRegistrationStore>();
        services.AddSingleton<FileDeviceRegistrationStore>();
        services.AddSingleton<IDeviceRegistrationStore>(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DeviceRegistrationOptions>>().Value;
            return ProviderNames.IsFile(options.Provider)
                ? sp.GetRequiredService<FileDeviceRegistrationStore>()
                : sp.GetRequiredService<InMemoryDeviceRegistrationStore>();
        });
        services.AddSingleton<DeviceRegistrationService>();
        services.AddHostedService<DeviceRegistrationInitializationHostedService>();
        services.AddSingleton<IDataNormalizer, DataNormalizer>();
        services.AddSingleton<DeviceTelemetryFilter>();
        services.AddSingleton<IngestionService>();

        return services;
    }
}
