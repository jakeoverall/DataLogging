using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using DataLogging.Ingestion.Abstractions;
using DataLogging.Ingestion.Models;

namespace DataLogging.Api.Ingestion;

public sealed class WebSocketDeviceDataSource : IDataSource, IAsyncDisposable
{
    private readonly DeviceDefinition _device;
    private readonly ClientWebSocket _socket;
    private readonly WebSocketConnectionTracker _tracker;

    public WebSocketDeviceDataSource(DeviceDefinition device, WebSocketConnectionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(tracker);

        _device = device;
        _tracker = tracker;
        _socket = new ClientWebSocket();
    }

    public string SourceId => $"websocket:{_device.DeviceId}";

    public DeviceProtocol Protocol => DeviceProtocol.WebSocket;

    public async IAsyncEnumerable<RawData> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var uri = WebSocketDeviceConnectionFactory.CreateUri(_device);
        await _socket.ConnectAsync(uri, cancellationToken).ConfigureAwait(false);
        _tracker.MarkConnected(_device.DeviceId, DateTimeOffset.UtcNow);

        var buffer = new byte[8192];
        while (_socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var payload = new List<byte>();
            var receiveResult = default(ValueWebSocketReceiveResult);
            do
            {
                receiveResult = await _socket.ReceiveAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    _tracker.MarkDisconnected(_device.DeviceId, DateTimeOffset.UtcNow);
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by server.", CancellationToken.None).ConfigureAwait(false);
                    yield break;
                }

                payload.AddRange(buffer.AsSpan(0, receiveResult.Count).ToArray());
                _tracker.MarkReceived(_device.DeviceId, DateTimeOffset.UtcNow);
            }
            while (!receiveResult.EndOfMessage);

            var payloadBytes = payload.ToArray();
            yield return new RawData
            {
                DeviceId = _device.DeviceId,
                Protocol = DeviceProtocol.WebSocket,
                DataType = GetDataType(),
                Payload = payloadBytes,
                SourceTimestamp = DateTimeOffset.UtcNow,
                SequenceNumber = 0,
                ReceivedTimestamp = DateTimeOffset.UtcNow
            };
        }

        _tracker.MarkDisconnected(_device.DeviceId, DateTimeOffset.UtcNow);
    }

    public async ValueTask DisposeAsync()
    {
        _tracker.MarkDisconnected(_device.DeviceId, DateTimeOffset.UtcNow);

        if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseSent)
        {
            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Shutting down.", CancellationToken.None).ConfigureAwait(false);
            return;
        }

        _socket.Dispose();
    }

    private string GetDataType()
    {
        if (_device.Properties.TryGetValue("dataType", out var dataType)
            && !string.IsNullOrWhiteSpace(dataType))
        {
            return dataType;
        }

        return "websocket-message";
    }
}
