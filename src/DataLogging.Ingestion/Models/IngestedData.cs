using DataLogging.Core.Models;

namespace DataLogging.Ingestion.Models;

public sealed record IngestedData
{
    public required LogRecord<object> Record { get; init; }

    public required DeviceDefinition Device { get; init; }
}
