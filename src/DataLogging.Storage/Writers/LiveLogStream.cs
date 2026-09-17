using System.Runtime.CompilerServices;
using System.Threading.Channels;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Models;

namespace DataLogging.Storage.Writers;

public sealed class LiveLogStream
{
    private readonly Channel<LogRecordEnvelope> _channel = Channel.CreateUnbounded<LogRecordEnvelope>(
        new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

    public async ValueTask PublishAsync(LogRecordEnvelope record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        await _channel.Writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<LogRecordEnvelope> SubscribeAsync(
        string? deviceId = null,
        string? source = null,
        string? dataType = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!await WaitToReadSafelyAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }

            while (_channel.Reader.TryRead(out var record))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (deviceId is not null && !string.Equals(record.Metadata.DeviceId, deviceId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (source is not null && !string.Equals(record.Metadata.Source, source, StringComparison.Ordinal))
                {
                    continue;
                }

                if (dataType is not null && !string.Equals(record.Metadata.DataType, dataType, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return record;
            }
        }
    }

    private async ValueTask<bool> WaitToReadSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}

public sealed class LiveLogWriter : ILogWriter
{
    private readonly ILogWriter _inner;
    private readonly LiveLogStream _stream;

    public LiveLogWriter(ILogWriter inner, LiveLogStream stream)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(stream);

        _inner = inner;
        _stream = stream;
    }

    public async ValueTask WriteAsync(LogRecordEnvelope record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _inner.WriteAsync(record, cancellationToken).ConfigureAwait(false);
        await _stream.PublishAsync(record, cancellationToken).ConfigureAwait(false);
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
    {
        return _inner.FlushAsync(cancellationToken);
    }
}
