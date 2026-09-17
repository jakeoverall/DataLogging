using DataLogging.Ingestion.Models;

namespace DataLogging.Api.Ingestion;

public static class WebSocketDeviceConnectionFactory
{
    public static Uri CreateUri(DeviceDefinition device)
    {
        ArgumentNullException.ThrowIfNull(device);

        var rawAddress = device.Address?.Trim();
        var rawPort = device.Port?.Trim();

        if (string.IsNullOrWhiteSpace(rawAddress) && string.IsNullOrWhiteSpace(rawPort))
        {
            throw new InvalidOperationException($"Device '{device.DeviceId}' does not specify a websocket address.");
        }

        if (Uri.TryCreate(rawAddress, UriKind.Absolute, out var absoluteUri)
            && (absoluteUri.Scheme == Uri.UriSchemeWs || absoluteUri.Scheme == Uri.UriSchemeWss))
        {
            return absoluteUri;
        }

        var host = string.IsNullOrWhiteSpace(rawAddress)
            ? "localhost"
            : rawAddress;

        if (host.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(host, UriKind.Absolute, out var configuredUri))
            {
                return configuredUri;
            }
        }

        var port = TryParsePort(rawPort, host);
        var scheme = host.StartsWith("wss://", StringComparison.OrdinalIgnoreCase)
            ? Uri.UriSchemeWss
            : Uri.UriSchemeWs;

        var builder = new UriBuilder(scheme, host, port);
        return builder.Uri;
    }

    private static int TryParsePort(string? rawPort, string host)
    {
        if (!string.IsNullOrWhiteSpace(rawPort) && int.TryParse(rawPort, out var parsedPort))
        {
            return parsedPort;
        }

        if (host.Contains(':') && host.LastIndexOf(':') > host.LastIndexOf('/'))
        {
            var lastColonIndex = host.LastIndexOf(':');
            var candidate = host[(lastColonIndex + 1)..];
            if (int.TryParse(candidate, out var portFromHost))
            {
                return portFromHost;
            }
        }

        return 80;
    }
}
