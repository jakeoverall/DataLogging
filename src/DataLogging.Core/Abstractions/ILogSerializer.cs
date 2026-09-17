using DataLogging.Core.Models;

namespace DataLogging.Core.Abstractions;

public interface ILogSerializer
{
    LogRecordEnvelope Serialize<TPayload>(LogRecord<TPayload> record);

    LogRecord<TPayload> Deserialize<TPayload>(LogRecordEnvelope envelope);
}
