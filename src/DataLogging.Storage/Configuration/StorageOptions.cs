namespace DataLogging.Storage.Configuration;

public sealed record StorageOptions
{
    public string Provider { get; set; } = "memory";

    public string FilePath { get; set; } = "data/logs.ndjson";
}
