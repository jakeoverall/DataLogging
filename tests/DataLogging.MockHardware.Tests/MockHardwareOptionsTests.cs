using DataLogging.MockHardware.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace DataLogging.MockHardware.Tests;

public sealed class MockHardwareOptionsTests
{
    [Fact]
    public void Defaults_AreApplied_WhenValuesAreOmitted()
    {
        var options = BuildOptions();

        Assert.Equal(3, options.NDevices);
        Assert.Equal(TimeSpan.FromSeconds(1), options.EmitFrequency);
        Assert.False(options.LogRaw);
        Assert.Null(options.Duration);
        Assert.Null(options.Seed);
        Assert.Equal(64, options.BufferCapacity);
    }

    [Fact]
    public void ExplicitDeviceCount_IsParsed()
    {
        var options = BuildOptions(new KeyValuePair<string, string?>("NDevices", "10"));

        Assert.Equal(10, options.NDevices);
    }

    [Fact]
    public void InvalidDeviceCount_IsRejected()
    {
        var result = Validate(new MockHardwareOptions { NDevices = 0 });

        Assert.False(result.Succeeded);
    }

    [Theory]
    [InlineData("250ms", 250)]
    [InlineData("0.75s", 750)]
    [InlineData("1s", 1000)]
    public void EmitFrequency_IsParsed(string input, int expectedMilliseconds)
    {
        var options = BuildOptions(new KeyValuePair<string, string?>("EmitFrequency", input));

        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), options.EmitFrequency);
    }

    [Fact]
    public void InvalidFrequency_IsRejected()
    {
        var result = Validate(new MockHardwareOptions { EmitFrequency = TimeSpan.Zero });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Duration_IsParsed()
    {
        var options = BuildOptions(new KeyValuePair<string, string?>("Duration", "30s"));

        Assert.Equal(TimeSpan.FromSeconds(30), options.Duration);
    }

    [Fact]
    public void Seed_IsParsed()
    {
        var options = BuildOptions(new KeyValuePair<string, string?>("Seed", "42"));

        Assert.Equal(42, options.Seed);
    }

    private static MockHardwareOptions BuildOptions(params KeyValuePair<string, string?>[] overrides)
    {
        var values = new Dictionary<string, string?>
        {
            ["NDevices"] = "3",
            ["EmitFrequency"] = "1s",
            ["BufferCapacity"] = "64"
        };

        foreach (var (key, value) in overrides)
        {
            values[key] = value;
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new MockHardwareOptions();
        new MockHardwareOptionsSetup(config).Configure(options);
        return options;
    }

    private static ValidateOptionsResult Validate(MockHardwareOptions options)
    {
        return new MockHardwareOptionsValidator().Validate(Options.DefaultName, options);
    }
}
