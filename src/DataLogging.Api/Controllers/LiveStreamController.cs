using System.Text;
using DataLogging.Api.Infrastructure;
using DataLogging.Storage.Writers;
using Microsoft.AspNetCore.Mvc;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/streams")]
public sealed class LiveStreamController : ControllerBase
{
    private readonly LiveLogStream _stream;

    public LiveStreamController(LiveLogStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
    }

    [HttpGet("live")]
    public async Task StreamLiveLogsAsync(
        string? deviceId,
        string? source,
        string? dataType,
        CancellationToken cancellationToken)
    {
        try
        {
            SseResponseWriter.Configure(Response);
            await SseResponseWriter.WriteEventAsync(Response, "connected", new { status = "connected" }, cancellationToken).ConfigureAwait(false);

            await foreach (var record in _stream.SubscribeAsync(deviceId, source, dataType, cancellationToken).WithCancellation(cancellationToken))
            {
                var payloadText = record.Payload.Length > 0 ? Encoding.UTF8.GetString(record.Payload.Span) : string.Empty;
                var payload = new
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

                await SseResponseWriter.WriteEventAsync(Response, "log", payload, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // SSE clients cancel the request when navigating away; treat as normal completion.
        }
    }
}
