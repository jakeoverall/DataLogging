using DataLogging.MockHardware.Abstractions;
using DataLogging.MockHardware.Registry;
using Microsoft.AspNetCore.Mvc;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/mock-hardware")]
public sealed class MockHardwareApiController : ControllerBase
{
    private readonly IServiceProvider _services;

    public MockHardwareApiController(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
    }

    [HttpGet("devices")]
    public IActionResult GetDevices()
    {
        var registry = _services.GetService<MockDeviceRegistry>();
        return Ok(registry is null ? Array.Empty<object>() : registry.GetDevices());
    }

    [HttpGet("states")]
    public IActionResult GetStates()
    {
        var registry = _services.GetService<MockDeviceRegistry>();
        return Ok(registry is null ? Array.Empty<object>() : registry.GetDeviceStates());
    }

    [HttpGet("control")]
    public IActionResult GetControlState()
    {
        var controller = _services.GetService<IMockHardwareController>();
        if (controller is null)
        {
            return Ok(new { enabled = false, state = "disabled", reason = "Runtime.UseMockData is false." });
        }

        return Ok(new { enabled = true, status = controller.GetStatus() });
    }

    [HttpPost("control/start")]
    public IActionResult Start()
    {
        var controller = _services.GetService<IMockHardwareController>();
        if (controller is null)
        {
            return Conflict(new { error = "Mock hardware is disabled by configuration." });
        }

        return Ok(controller.Start());
    }

    [HttpPost("control/pause")]
    public IActionResult Pause()
    {
        var controller = _services.GetService<IMockHardwareController>();
        if (controller is null)
        {
            return Conflict(new { error = "Mock hardware is disabled by configuration." });
        }

        return Ok(controller.Pause());
    }

    [HttpPost("control/stop")]
    public IActionResult Stop()
    {
        var controller = _services.GetService<IMockHardwareController>();
        if (controller is null)
        {
            return Conflict(new { error = "Mock hardware is disabled by configuration." });
        }

        return Ok(controller.Stop());
    }
}
