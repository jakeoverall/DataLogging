using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Configuration;

public sealed class MockHardwareOptionsValidator : IValidateOptions<MockHardwareOptions>
{
    public ValidateOptionsResult Validate(string? name, MockHardwareOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.NDevices <= 0)
        {
            failures.Add("The n_devices value must be greater than zero.");
        }

        if (options.EmitFrequency <= TimeSpan.Zero)
        {
            failures.Add("The emit_frequency value must be greater than zero.");
        }

        if (options.Duration is { } duration && duration <= TimeSpan.Zero)
        {
            failures.Add("The duration value must be greater than zero when specified.");
        }

        if (options.BufferCapacity <= 0)
        {
            failures.Add("The buffer_capacity value must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
