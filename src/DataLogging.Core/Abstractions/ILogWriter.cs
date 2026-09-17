using DataLogging.Core.Models;

namespace DataLogging.Core.Abstractions;

public interface ILogWriter
{
    ValueTask WriteAsync(LogRecordEnvelope record, CancellationToken cancellationToken = default);

    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
