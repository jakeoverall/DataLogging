using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Abstractions;

public interface IMockCanOpenSource
{
    IAsyncEnumerable<RawCanOpenMessage> ReadAsync(CancellationToken cancellationToken = default);
}
