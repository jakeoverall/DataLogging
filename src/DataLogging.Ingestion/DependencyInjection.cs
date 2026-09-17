using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Normalization;
using DataLogging.Ingestion.Registry;
using Microsoft.Extensions.DependencyInjection;

namespace DataLogging.Ingestion;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLoggingIngestion(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<IDataNormalizer, DataNormalizer>();
        services.AddSingleton<IngestionService>();

        return services;
    }
}
