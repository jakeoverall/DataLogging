using DataLogging.Core.Models;
using DataLogging.Core.Queries;

namespace DataLogging.Core.Abstractions;

public interface ILogReader
{
    IAsyncEnumerable<LogRecordEnvelope> ReadAsync(LogQuery query, CancellationToken cancellationToken = default);
}
