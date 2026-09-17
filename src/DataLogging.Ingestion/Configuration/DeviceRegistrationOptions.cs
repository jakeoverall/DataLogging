namespace DataLogging.Ingestion.Configuration;

public sealed record DeviceRegistrationOptions
{
    public string Provider { get; set; } = "memory";

    public string FilePath { get; set; } = "config/device-registry.json";
}
