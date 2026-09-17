using System.Threading.Channels;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Models;
using Microsoft.Extensions.Options;

namespace DataLogging.MockHardware.Sources;

public sealed class MockRos2Source : IMockRos2Source
{
    private readonly Channel<RawRos2Message> _channel;

    public MockRos2Source(IOptions<MockHardwareOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _channel = Channel.CreateBounded<RawRos2Message>(new BoundedChannelOptions(options.Value.BufferCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait,
            AllowSynchronousContinuations = false
        });
    }

    public IAsyncEnumerable<RawRos2Message> ReadAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    internal ValueTask PublishAsync(RawRos2Message message, CancellationToken cancellationToken)
    {
        return _channel.Writer.WriteAsync(message, cancellationToken);
    }

    internal void Complete(Exception? error = null)
    {
        _channel.Writer.TryComplete(error);
    }
}
