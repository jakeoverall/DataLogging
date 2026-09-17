using DataLogging.Core.Abstractions;
using DataLogging.Storage.Configuration;
using DataLogging.Storage.Serialization;
using DataLogging.Storage.Writers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DataLogging.Storage;

public static class DependencyInjection
{
    public static IServiceCollection AddDataLoggingStorage(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IValidateOptions<StorageOptions>, StorageOptionsValidator>();
        services.AddOptions<StorageOptions>().ValidateOnStart();

        services.AddSingleton<InMemoryLogStore>();
        services.AddSingleton<NdjsonLogStore>();
        services.AddSingleton<LiveLogStream>();
        services.AddSingleton<ILogWriter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            ILogWriter inner = StorageProviderNames.IsFile(options.Provider)
                ? sp.GetRequiredService<NdjsonLogStore>()
                : sp.GetRequiredService<InMemoryLogStore>();
            return new LiveLogWriter(inner, sp.GetRequiredService<LiveLogStream>());
        });
        services.AddSingleton<ILogReader>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            return StorageProviderNames.IsFile(options.Provider)
                ? sp.GetRequiredService<NdjsonLogStore>()
                : sp.GetRequiredService<InMemoryLogStore>();
        });
        services.AddSingleton<ILogSerializer, JsonLogSerializer>();

        return services;
    }
}
