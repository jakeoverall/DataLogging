using DataLogging.Api.Configuration;
using DataLogging.Api.Ingestion;
using DataLogging.Api.Models;
using DataLogging.Ingestion;
using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Registry;
using DataLogging.Storage;
using DataLogging.Storage.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(
    EnvFileConfigurationLoader.Load(".env"));

builder.Services.AddOpenApi();

builder.Services.Configure<DeviceRegistrationOptions>(
    builder.Configuration.GetSection("DeviceRegistration"));
builder.Services.Configure<StorageOptions>(
    builder.Configuration.GetSection("Storage"));
builder.Services.Configure<MockHardwareOptions>(
    builder.Configuration.GetSection("MockHardware"));

builder.Services.AddDataLoggingIngestion();
builder.Services.AddDataLoggingStorage();
builder.Services.AddMockHardware(builder.Configuration.GetSection("MockHardware"), stopHostOnCompletion: false);

builder.Services.AddSingleton<MockRos2IngestionDataSource>();
builder.Services.AddSingleton<MockCanOpenIngestionDataSource>();
builder.Services.AddSingleton<MockEthernetIngestionDataSource>();
builder.Services.AddHostedService<MockHardwareDeviceBootstrapHostedService>();
builder.Services.AddHostedService<MockHardwareIngestionBridgeHostedService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.MapGet("/api/mock-hardware/devices", (MockDeviceRegistry registry) =>
{
    return Results.Ok(registry.GetDevices());
});

app.MapGet("/api/mock-hardware/states", (MockDeviceRegistry registry) =>
{
    return Results.Ok(registry.GetDeviceStates());
});

app.Run();
