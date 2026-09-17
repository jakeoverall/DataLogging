using DataLogging.Api.Configuration;
using DataLogging.Api.Ingestion;
using DataLogging.Api.Models;
using DataLogging.Core.Abstractions;
using DataLogging.Core.Queries;
using DataLogging.Ingestion;
using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Registry;
using System.Text;
using System.Text.Json;
using DataLogging.Storage;
using DataLogging.Storage.Configuration;
using DataLogging.Storage.Writers;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(
    EnvFileConfigurationLoader.Load(".env"));

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularDevClient", policy =>
    {
        policy
            .WithOrigins("http://localhost:4200", "https://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.Configure<DeviceRegistrationOptions>(
    builder.Configuration.GetSection("DeviceRegistration"));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));
builder.Services.Configure<MockHardwareOptions>(
    builder.Configuration.GetSection("MockHardware"));
builder.Services.Configure<RuntimeOptions>(
    builder.Configuration.GetSection("Runtime"));

var useMockData = builder.Configuration.GetValue<bool?>("Runtime:UseMockData") ?? true;

builder.Services.AddDataLoggingIngestion();
builder.Services.AddDataLoggingStorage();
if (useMockData)
{
    builder.Services.AddMockHardware(builder.Configuration.GetSection("MockHardware"), stopHostOnCompletion: false);

    builder.Services.AddSingleton<MockRos2IngestionDataSource>();
    builder.Services.AddSingleton<MockCanOpenIngestionDataSource>();
    builder.Services.AddSingleton<MockEthernetIngestionDataSource>();
    builder.Services.AddHostedService<MockHardwareDeviceBootstrapHostedService>();
    builder.Services.AddHostedService<MockHardwareIngestionBridgeHostedService>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("AllowAngularDevClient");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Data-Mode"] = useMockData ? "mock" : "live";
    await next();
});

app.MapGet("/api/runtime/mode", () =>
{
    return Results.Ok(new
    {
        useMockData,
        dataMode = useMockData ? "mock" : "live"
    });
});

app.MapGet("/api/devices", (DeviceRegistrationService service) =>
{
    return Results.Ok(service.GetAll());
});

app.MapPut("/api/devices/{deviceId}", async (
    string deviceId,
    UpsertDeviceRequest request,
    DeviceRegistrationService service,
    CancellationToken cancellationToken) =>
{
    var device = new DeviceDefinition
    {
        DeviceId = deviceId,
        Name = request.Name,
        DeviceType = request.DeviceType,
        Protocol = request.Protocol,
        Address = request.Address,
        Port = request.Port,
        Enabled = request.Enabled,
        Properties = request.Properties ?? new Dictionary<string, string>()
    };

    await service.UpsertAsync(device, cancellationToken);
    return Results.NoContent();
});

app.MapDelete("/api/devices/{deviceId}", async (
    string deviceId,
    DeviceRegistrationService service,
    CancellationToken cancellationToken) =>
{
    var removed = await service.RemoveAsync(deviceId, cancellationToken);
    return removed ? Results.NoContent() : Results.NotFound();
});

app.MapGet("/api/mock-hardware/devices", (IServiceProvider services) =>
{
    var registry = services.GetService<MockDeviceRegistry>();
    return registry is null
        ? Results.Ok(Array.Empty<object>())
        : Results.Ok(registry.GetDevices());
});

app.MapGet("/api/mock-hardware/states", (IServiceProvider services) =>
{
    var registry = services.GetService<MockDeviceRegistry>();
    return registry is null
        ? Results.Ok(Array.Empty<object>())
        : Results.Ok(registry.GetDeviceStates());
});

app.MapGet("/api/mock-hardware/control", (IServiceProvider services) =>
{
    var controller = services.GetService<IMockHardwareController>();
    if (controller is null)
    {
        return Results.Ok(new { enabled = false, state = "disabled", reason = "Runtime.UseMockData is false." });
    }

    return Results.Ok(new { enabled = true, status = controller.GetStatus() });
});

app.MapPost("/api/mock-hardware/control/start", (IServiceProvider services) =>
{
    var controller = services.GetService<IMockHardwareController>();
    if (controller is null)
    {
        return Results.Conflict(new { error = "Mock hardware is disabled by configuration." });
    }

    return Results.Ok(controller.Start());
});

app.MapPost("/api/mock-hardware/control/pause", (IServiceProvider services) =>
{
    var controller = services.GetService<IMockHardwareController>();
    if (controller is null)
    {
        return Results.Conflict(new { error = "Mock hardware is disabled by configuration." });
    }

    return Results.Ok(controller.Pause());
});

app.MapPost("/api/mock-hardware/control/stop", (IServiceProvider services) =>
{
    var controller = services.GetService<IMockHardwareController>();
    if (controller is null)
    {
        return Results.Conflict(new { error = "Mock hardware is disabled by configuration." });
    }

    return Results.Ok(controller.Stop());
});

app.MapGet("/api/logs", async (
    ILogReader reader,
    string? deviceId,
    string? source,
    string? dataType,
    int? limit,
    CancellationToken cancellationToken) =>
{
    var query = new LogQuery
    {
        DeviceId = deviceId,
        Source = source,
        DataType = dataType
    };

    var records = new List<object>();
    var max = Math.Clamp(limit ?? 100, 1, 1000);
    await foreach (var record in reader.ReadAsync(query, cancellationToken).WithCancellation(cancellationToken))
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

    return Results.Ok(records);
});

app.MapGet("/api/ingestion/status", (DeviceRegistrationService deviceService, IServiceProvider services) =>
{
    var states = services.GetService<MockDeviceRegistry>()?.GetDeviceStates();
    return Results.Ok(new
    {
        useMockData,
        registeredDevices = deviceService.GetAll().Count,
        mockDeviceStates = states
    });
});

app.MapGet("/api/streams/live", async (
    LiveLogStream stream,
    HttpContext context,
    string? deviceId,
    string? source,
    string? dataType,
    CancellationToken cancellationToken) =>
{
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";
    context.Response.Headers["X-Accel-Buffering"] = "no";
    context.Response.ContentType = "text/event-stream";

    await context.Response.WriteAsync("event: connected\ndata: {\"status\":\"connected\"}\n\n", cancellationToken).ConfigureAwait(false);
    await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);

    await foreach (var record in stream.SubscribeAsync(deviceId, source, dataType, cancellationToken).WithCancellation(cancellationToken))
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

        var eventData = JsonSerializer.Serialize(payload);
        await context.Response.WriteAsync($"event: log\ndata: {eventData}\n\n", cancellationToken).ConfigureAwait(false);
        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
});

app.Run();
