using DataLogging.Api.Ingestion;
using DataLogging.Ingestion.Models;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class WebSocketDeviceConnectionFactoryTests
{
    [Fact]
    public void BuildUri_UsesWebSocketScheme_ForRegisteredWebSocketDevice()
    {
        var device = new DeviceDefinition
        {
            DeviceId = "turtlesim001",
            Name = "ROS Turtle",
            DeviceType = "Custom gateway",
            Protocol = DeviceProtocol.WebSocket,
            Address = "192.168.1.220",
            Port = "8765",
            Enabled = true,
            Properties = new Dictionary<string, string>()
        };

        var uri = WebSocketDeviceConnectionFactory.CreateUri(device);

        Assert.Equal(Uri.UriSchemeWs, uri.Scheme);
        Assert.Equal("192.168.1.220", uri.Host);
        Assert.Equal(8765, uri.Port);
    }
}
