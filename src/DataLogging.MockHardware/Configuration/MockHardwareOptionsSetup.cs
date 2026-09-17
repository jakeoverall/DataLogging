using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Configuration;

public sealed class MockHardwareOptionsSetup : IConfigureOptions<MockHardwareOptions>
{
    private readonly IConfiguration _configuration;

    public MockHardwareOptionsSetup(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        _configuration = configuration;
    }

    public void Configure(MockHardwareOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.NDevices = GetInt("NDevices", options.NDevices);
        options.EmitFrequency = GetDuration("EmitFrequency", options.EmitFrequency);
        options.LogRaw = GetBool("LogRaw", options.LogRaw);
        options.Duration = GetNullableDuration("Duration", options.Duration);
        options.Seed = GetNullableInt("Seed", options.Seed);
        options.BufferCapacity = GetInt("BufferCapacity", options.BufferCapacity);
    }

    private int GetInt(string key, int fallback)
    {
        var value = _configuration[key];
        return value is null ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private int? GetNullableInt(string key, int? fallback)
    {
        var value = _configuration[key];
        return value is null ? fallback : int.Parse(value, CultureInfo.InvariantCulture);
    }

    private bool GetBool(string key, bool fallback)
    {
        var value = _configuration[key];
        return value is null ? fallback : bool.Parse(value);
    }

    private TimeSpan GetDuration(string key, TimeSpan fallback)
    {
        var value = _configuration[key];
        return value is null ? fallback : DurationParser.Parse(value, key);
    }

    private TimeSpan? GetNullableDuration(string key, TimeSpan? fallback)
    {
        var value = _configuration[key];
        return value is null ? fallback : DurationParser.Parse(value, key);
    }
}
