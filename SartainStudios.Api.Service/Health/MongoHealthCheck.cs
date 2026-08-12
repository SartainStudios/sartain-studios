using Microsoft.Extensions.Diagnostics.HealthChecks;
using MongoDB.Driver;

namespace SartainStudios.Api.Service.Health;

public sealed class MongoHealthCheck(IMongoClient mongoClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await mongoClient.ListDatabaseNamesAsync(cancellationToken);
            return HealthCheckResult.Healthy("MongoDB connection is healthy.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB connection failed.", exception);
        }
    }
}