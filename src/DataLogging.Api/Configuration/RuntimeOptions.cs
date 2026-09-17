namespace DataLogging.Api.Configuration;

public sealed record RuntimeOptions
{
    public bool UseMockData { get; set; } = true;
}
