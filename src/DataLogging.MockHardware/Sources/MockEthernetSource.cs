using System.Threading.Channels;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Models;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Sources;

public sealed class MockEthernetSource : IMockEthernetSource
{
    private readonly Channel<RawEthernetMessage> _channel;

    public MockEthernetSource(IOptions<MockHardwareOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<RawEthernetMessage>(new BoundedChannelOptions(options.Value.BufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public IAsyncEnumerable<RawEthernetMessage> ReadAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    internal ValueTask PublishAsync(RawEthernetMessage message, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    internal void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }
}
