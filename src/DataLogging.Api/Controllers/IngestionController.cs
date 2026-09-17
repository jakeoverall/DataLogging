using DataLogging.Api.Configuration;
using DataLogging.Ingestion.Registry;
using DataLogging.MockHardware.Registry;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/ingestion")]
public sealed class IngestionController : ControllerBase
{
    private readonly DeviceRegistrationService _deviceRegistrationService;
    private readonly IServiceProvider _services;
    private readonly RuntimeOptions _runtimeOptions;

    public IngestionController(
        DeviceRegistrationService deviceRegistrationService,
        IServiceProvider services,
        IOptions<RuntimeOptions> runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistrationService);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(runtimeOptions);

        _deviceRegistrationService = deviceRegistrationService;
        _services = services;
        _runtimeOptions = runtimeOptions.Value;
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var states = _services.GetService<MockDeviceRegistry>()?.GetDeviceStates();
        return Ok(new
        {
            useMockData = _runtimeOptions.UseMockData,
            registeredDevices = _deviceRegistrationService.GetAll().Count,
            mockDeviceStates = states
        });
    }
}
