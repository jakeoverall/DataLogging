using DataLogging.MockHardware.Models;

namespace DataLogging.MockHardware.Abstractions;

public interface IMockEthernetSource
{
    IAsyncEnumerable<RawEthernetMessage> ReadAsync(CancellationToken cancellationToken = default);
}
