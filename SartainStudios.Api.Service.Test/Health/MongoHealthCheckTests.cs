using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;
using NSubstitute;
using SartainStudios.Api.Service.Health;

namespace SartainStudios.Api.Service.Test.Health;

public sealed class MongoHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ReturnsHealthyWhenConnectionSucceeds()
    {
        var mongoClient = Substitute.For<IMongoClient>();
        mongoClient.ListDatabaseNamesAsync(Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAsyncCursor<string>>());
        var healthCheck = new MongoHealthCheck(mongoClient);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsUnhealthyWhenConnectionThrows()
    {
        var mongoClient = Substitute.For<IMongoClient>();
        mongoClient.ListDatabaseNamesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<IAsyncCursor<string>>>(_ => throw new TimeoutException());
        var healthCheck = new MongoHealthCheck(mongoClient);
        var context = new HealthCheckContext();

        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }
}