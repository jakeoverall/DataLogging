using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Abstractions;

public interface IDataSource
{
    string SourceId { get; }

    DeviceProtocol Protocol { get; }

    IAsyncEnumerable<RawData> ReadAsync(CancellationToken cancellationToken = default);
}
