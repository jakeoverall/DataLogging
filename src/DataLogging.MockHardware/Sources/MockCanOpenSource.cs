using System.Threading.Channels;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Models;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Sources;

public sealed class MockCanOpenSource : IMockCanOpenSource
{
    private readonly Channel<RawCanOpenMessage> _channel;

    public MockCanOpenSource(IOptions<MockHardwareOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<RawCanOpenMessage>(new BoundedChannelOptions(options.Value.BufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public IAsyncEnumerable<RawCanOpenMessage> ReadAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    internal ValueTask PublishAsync(RawCanOpenMessage message, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    internal void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }
}
