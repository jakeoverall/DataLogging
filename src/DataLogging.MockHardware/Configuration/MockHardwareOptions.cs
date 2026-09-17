namespace DataLogging.MockHardware.Configuration;

public sealed record MockHardwareOptions
{
    public int NDevices { get; set; } = 3;

    public TimeSpan EmitFrequency { get; set; } = TimeSpan.FromSeconds(1);

    public bool LogRaw { get; set; }

    public TimeSpan? Duration { get; set; }

    public int? Seed { get; set; }

    public int BufferCapacity { get; set; } = 64;
}
