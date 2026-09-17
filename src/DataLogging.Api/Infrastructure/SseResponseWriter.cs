using System.Text.Json;

namespace DataLogging.Api.Infrastructure;

internal static class SseResponseWriter
{
    public static void Configure(HttpResponse response)
    {
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        response.Headers["X-Accel-Buffering"] = "no";
        response.ContentType = "text/event-stream";
    }

    public static async Task WriteEventAsync(
        HttpResponse response,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(payload);

        var eventData = JsonSerializer.Serialize(payload);
        await response.WriteAsync($"event: {eventName}\ndata: {eventData}\n\n", cancellationToken).ConfigureAwait(false);
        await response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
