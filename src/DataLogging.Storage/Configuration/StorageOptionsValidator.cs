using Microsoft.Extensions.Options;

namespace DataLogging.Storage.Configuration;

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!StorageProviderNames.IsSupported(options.Provider))
        {
            return ValidateOptionsResult.Fail(
                $"Unsupported storage provider '{options.Provider}'. Supported values: {string.Join(", ", StorageProviderNames.SupportedValues)}.");
        }

        if (StorageProviderNames.IsFile(options.Provider) && string.IsNullOrWhiteSpace(options.FilePath))
        {
            return ValidateOptionsResult.Fail("A non-empty Storage:FilePath is required when provider is 'file'.");
        }

        return ValidateOptionsResult.Success;
    }
}
