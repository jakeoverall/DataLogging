namespace DataLogging.Storage.Configuration;

internal static class StorageProviderNames
{
    public const string Memory = "memory";
    public const string File = "file";

    public static readonly string[] SupportedValues = [Memory, File];

    public static bool IsSupported(string provider)
    {
        return string.Equals(provider, Memory, StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, File, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsFile(string provider)
    {
        return string.Equals(provider, File, StringComparison.OrdinalIgnoreCase);
    }
}
