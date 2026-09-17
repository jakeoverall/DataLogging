namespace DataLogging.Api.Configuration;

internal static class EnvFileConfigurationLoader
{
    public static IReadOnlyDictionary<string, string?> Load(string envFilePath)
    {
        if (!File.Exists(envFilePath))
        {
            return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        }

        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadAllLines(envFilePath))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        return values;
    }
}
