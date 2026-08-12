using Microsoft.AspNetCore.Mvc;
using SartainStudios.Api.Service.Health;
using SartainStudios.Schema.Health;

namespace SartainStudios.Api.Controller;

[ApiController]
[Route("api/[controller]")]
public sealed class HealthController(HealthService healthService) : ControllerBase
{
    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthReportResponse> Live()
    {
        return Ok(healthService.CheckLiveness());
    }

    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReportResponse>> Ready(CancellationToken cancellationToken)
    {
        var report = await healthService.CheckReadinessAsync(cancellationToken);
        return WriteReport(report);
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReportResponse>> Get(CancellationToken cancellationToken)
    {
        var report = await healthService.CheckAsync(cancellationToken);
        return WriteReport(report);
    }

    private ActionResult<HealthReportResponse> WriteReport(HealthReportResponse report)
    {
        return string.Equals(report.Status, "Healthy", StringComparison.Ordinal)
            ? Ok(report)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, report);
    }
}