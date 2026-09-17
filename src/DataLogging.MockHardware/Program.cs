using DataLogging.MockHardware.Configuration;
using DataLogging.MockHardware.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddCommandLine(args, new Dictionary<string, string>
{
    ["--n_devices"] = nameof(MockHardwareOptions.NDevices),
    ["--emit_frequency"] = nameof(MockHardwareOptions.EmitFrequency),
    ["--log_raw"] = nameof(MockHardwareOptions.LogRaw),
    ["--duration"] = nameof(MockHardwareOptions.Duration),
    ["--seed"] = nameof(MockHardwareOptions.Seed),
    ["--buffer_capacity"] = nameof(MockHardwareOptions.BufferCapacity)
});

builder.Services.AddMockHardware(builder.Configuration);

await builder.Build().RunAsync();
