using DataLogging.Core.Abstractions;
using DataLogging.Core.Queries;
using Microsoft.AspNetCore.Mvc;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController : ControllerBase
{
    private readonly ILogReader _reader;

    public LogsController(ILogReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        _reader = reader;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogsAsync(
        string? deviceId,
        string? source,
        string? dataType,
        int? limit,
        CancellationToken cancellationToken)
    {
        var query = new LogQuery
        {
            DeviceId = deviceId,
            Source = source,
            DataType = dataType
        };

        var records = new List<object>();
        var max = Math.Clamp(limit ?? 100, 1, 1000);
        await foreach (var record in _reader.ReadAsync(query, cancellationToken).WithCancellation(cancellationToken))
        {
            records.Add(new
            {
                record.Metadata.RecordId,
                record.Metadata.DeviceId,
                record.Metadata.Source,
                record.Metadata.DataType,
                record.Metadata.RecordedTimestamp,
                record.Metadata.SourceTimestamp,
                record.Metadata.SequenceNumber,
                payloadBase64 = Convert.ToBase64String(record.Payload.Span)
            });

            if (records.Count >= max)
            {
                break;
            }
        }

        return Ok(records);
    }
}
