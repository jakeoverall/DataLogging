using DataLogging.Api.Configuration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DataLogging.Api.Controllers;

[ApiController]
[Route("api/runtime")]
public sealed class RuntimeController : ControllerBase
{
    private readonly RuntimeOptions _runtimeOptions;

    public RuntimeController(IOptions<RuntimeOptions> runtimeOptions)
    {
        ArgumentNullException.ThrowIfNull(runtimeOptions);
        _runtimeOptions = runtimeOptions.Value;
    }

    [HttpGet("mode")]
    public IActionResult GetMode()
    {
        var useMockData = _runtimeOptions.UseMockData;
        return Ok(new
        {
            useMockData,
            dataMode = useMockData ? "mock" : "live"
        });
    }
}
