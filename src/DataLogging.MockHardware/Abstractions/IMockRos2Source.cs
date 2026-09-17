using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Abstractions;

public interface IMockRos2Source
{
    IAsyncEnumerable<RawRos2Message> ReadAsync(CancellationToken cancellationToken = default);
}
