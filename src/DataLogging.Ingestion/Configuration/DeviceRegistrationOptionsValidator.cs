using Microsoft.Extensions.Options;

namespace DataLogging.Ingestion.Configuration;

public sealed class DeviceRegistrationOptionsValidator : IValidateOptions<DeviceRegistrationOptions>
{
    public ValidateOptionsResult Validate(string? name, DeviceRegistrationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!ProviderNames.IsSupported(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"Unsupported device registration provider '{options.Provider}'. Supported values: {string.Join(", ", ProviderNames.SupportedValues)}.");
        }

        if (ProviderNames.IsFile(options.Provider) && string.IsNullOrWhiteSpace(options.FilePath))
        {
            return ValidateOptionsResult.Fail("A non-empty DeviceRegistration:FilePath is required when provider is 'file'.");
        }

        return ValidateOptionsResult.Success;
    }
}
