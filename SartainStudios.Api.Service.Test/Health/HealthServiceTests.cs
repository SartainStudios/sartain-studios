using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using SartainStudios.Api.Service.Health;

namespace SartainStudios.Api.Service.Test.Health;

public sealed class HealthServiceTests
{
    [Fact]
    public void CheckLiveness_ReturnsLiveResponse()
    {
        var healthCheckService = Substitute.For<HealthCheckService>();
        var service = new HealthService(healthCheckService);

        var result = service.CheckLiveness();

        Assert.Equal("Healthy", result.Status);
    }

    [Fact]
    public async Task CheckAsync_ReturnsMappedReport()
    {
        var healthCheckService = Substitute.For<HealthCheckService>();
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["mongo"] = new(HealthStatus.Healthy, "ok", TimeSpan.FromSeconds(1), null, null)
        };
        var report = new HealthReport(entries, TimeSpan.FromSeconds(1));
        healthCheckService
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);
        var service = new HealthService(healthCheckService);

        var result = await service.CheckAsync(CancellationToken.None);

        Assert.Equal("Healthy", result.Status);
        Assert.Single(result.Checks);
        Assert.Equal("mongo", result.Checks[0].Name);
    }

    [Fact]
    public async Task CheckReadinessAsync_ReturnsMappedReport()
    {
        var healthCheckService = Substitute.For<HealthCheckService>();
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);
        healthCheckService
            .CheckHealthAsync(Arg.Any<Func<HealthCheckRegistration, bool>>(), Arg.Any<CancellationToken>())
            .Returns(report);
        var service = new HealthService(healthCheckService);

        var result = await service.CheckReadinessAsync(CancellationToken.None);

        Assert.Equal("Healthy", result.Status);
        Assert.Empty(result.Checks);
    }
}