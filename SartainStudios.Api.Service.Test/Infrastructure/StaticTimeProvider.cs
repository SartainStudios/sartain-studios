namespace SartainStudios.Api.Service.Test.Infrastructure;

internal sealed class StaticTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow()
    {
        return now;
    }
}