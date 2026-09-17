using System.Net;
using DataLogging.Api.Controllers;
using DataLogging.Api.Ingestion;
using DataLogging.Ingestion.Models;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Models;
using DataLogging.MockHardware.Registry;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DataLogging.Ingestion.Tests;

public sealed class DevicesControllerCancellationTests
{
    [Fact]
    public async Task StreamDevicesAsync_WhenCancellationRequested_DoesNotThrow()
    {
        var services = new ServiceCollection();
        services.AddSingleton<MockDeviceRegistry>();

        var serviceProvider = services.BuildServiceProvider();
        var registry = new DeviceRegistry();
        var store = new InMemoryDeviceRegistrationStore();
        var registrationService = new DeviceRegistrationService(registry, store);
        var connectionTracker = new WebSocketConnectionTracker();
        var controller = new DevicesController(registrationService, serviceProvider, connectionTracker);

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await controller.StreamDevicesAsync(cts.Token);
    }
}
