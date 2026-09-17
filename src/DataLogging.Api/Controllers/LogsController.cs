using DataLogging.Core.Abstractions;
using DataLogging.Core.Queries;
using DataLogging.Storage.Writers;
using DataLogging.Api.Infrastructure;
using System.Text;
using Microsoft.AspNetCore.Mvc;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/logs")]
public sealed class LogsController : ControllerBase
{
    private readonly ILogReader _reader;
    private readonly LiveLogStream _liveLogStream;

    public LogsController(ILogReader reader, LiveLogStream liveLogStream)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(liveLogStream);
        _reader = reader;
        _liveLogStream = liveLogStream;
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
            records.Add(ToLogPayload(record));

            if (records.Count >= max)
            {
                break;
            }
        }

        return Ok(records);
    }

    [HttpGet("stream")]
    public async Task StreamLogsAsync(
        [FromQuery] string deviceId,
        string? source,
        string? dataType,
        int? batchSize,
        int? flushIntervalMs,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "deviceId is required." }, cancellationToken).ConfigureAwait(false);
            return;
        }

        var maxBatchSize = Math.Clamp(batchSize ?? 25, 1, 200);
        var flushWindow = TimeSpan.FromMilliseconds(Math.Clamp(flushIntervalMs ?? 250, 50, 2_000));

        SseResponseWriter.Configure(Response);
        try
        {
            await SseResponseWriter.WriteEventAsync(
                Response,
                "connected",
                new
                {
                    status = "connected",
                    deviceId,
                    batchSize = maxBatchSize,
                    flushIntervalMs = (int)flushWindow.TotalMilliseconds
                },
                cancellationToken).ConfigureAwait(false);

            var batch = new List<object>(maxBatchSize);
            var nextFlushAt = DateTimeOffset.UtcNow.Add(flushWindow);

            await foreach (var record in _liveLogStream.SubscribeAsync(deviceId, source, dataType, cancellationToken).WithCancellation(cancellationToken))
            {
                batch.Add(ToLogPayload(record));
                var now = DateTimeOffset.UtcNow;
                if (batch.Count < maxBatchSize && now < nextFlushAt)
                {
                    continue;
                }

                await SseResponseWriter.WriteEventAsync(
                    Response,
                    "logs",
                    new
                    {
                        deviceId,
                        count = batch.Count,
                        items = batch.ToArray()
                    },
                    cancellationToken).ConfigureAwait(false);

                batch.Clear();
                nextFlushAt = now.Add(flushWindow);
            }

            if (batch.Count > 0)
            {
                await SseResponseWriter.WriteEventAsync(
                    Response,
                    "logs",
                    new
                    {
                        deviceId,
                        count = batch.Count,
                        items = batch.ToArray()
                    },
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // SSE clients cancel the request when navigating away; treat as normal completion.
        }
    }

    private static object ToLogPayload(DataLogging.Core.Models.LogRecordEnvelope record)
    {
        var payloadText = record.Payload.Length > 0 ? Encoding.UTF8.GetString(record.Payload.Span) : string.Empty;
        return new
        {
            metadata = new
            {
                record.Metadata.RecordId,
                record.Metadata.VehicleId,
                record.Metadata.MissionId,
                record.Metadata.DeviceId,
                record.Metadata.Source,
                record.Metadata.DataType,
                record.Metadata.Schema,
                record.Metadata.SchemaVersion,
                record.Metadata.SourceTimestamp,
                record.Metadata.RecordedTimestamp,
                record.Metadata.SequenceNumber,
                record.Metadata.Kind,
                record.Metadata.Priority
            },
            payloadBase64 = Convert.ToBase64String(record.Payload.Span),
            payloadText
        };
    }
}
