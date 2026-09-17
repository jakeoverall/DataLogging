using DataLogging.Core.Models;
using DataLogging.Ingestion.Models;

namespace DataLogging.Ingestion.Abstractions;

public interface IDataNormalizer
{
    bool CanNormalize(RawData data, DeviceDefinition device);

    LogRecord<object> Normalize(RawData data, DeviceDefinition device);
}
