using DataLogging.Api.Configuration;
using DataLogging.Api.Ingestion;
using DataLogging.Ingestion;
using DataLogging.Ingestion.Configuration;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Configuration;
using DataLogging.Storage;
using DataLogging.Storage.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddInMemoryCollection(
    EnvFileConfigurationLoader.Load(".env"));

builder.Services.AddControllers();
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
builder.Services.Configure<RuntimeOptions>(
    builder.Configuration.GetSection("Runtime"));

var useMockData = builder.Configuration.GetValue<bool?>("Runtime:UseMockData") ?? true;

builder.Services.AddDataLoggingIngestion();
builder.Services.AddDataLoggingStorage();
builder.Services.AddSingleton<WebSocketConnectionTracker>();
if (useMockData)
{
    builder.Services.AddMockHardware(builder.Configuration.GetSection("MockHardware"), stopHostOnCompletion: false);

    builder.Services.AddSingleton<MockRos2IngestionDataSource>();
    builder.Services.AddSingleton<MockCanOpenIngestionDataSource>();
    builder.Services.AddSingleton<MockEthernetIngestionDataSource>();
    builder.Services.AddHostedService<MockHardwareDeviceBootstrapHostedService>();
    builder.Services.AddHostedService<MockHardwareIngestionBridgeHostedService>();
}

builder.Services.AddHostedService<WebSocketDeviceBridgeHostedService>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseCors("AllowAngularDevClient");
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Data-Mode"] = useMockData ? "mock" : "live";
    await next();
});
app.MapControllers();

app.UseDefaultFiles();
app.UseStaticFiles();

app.Run();
