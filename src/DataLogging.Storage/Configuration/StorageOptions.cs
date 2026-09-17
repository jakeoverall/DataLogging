namespace DataLogging.Storage.Configuration;

public sealed record StorageOptions
{
    public string Provider { get; set; } = "memory";

    public string FilePath { get; set; } = "data/logs.ndjson";

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    public int MaxRetainedFiles { get; set; } = 5;
}
