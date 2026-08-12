namespace SartainStudios.Client.Service.Caching;

public sealed record CachePolicy(TimeSpan StaleAfter, TimeSpan ExpiresAfter)
{
    public static readonly CachePolicy Default = new(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));

    public static readonly CachePolicy Reference = new(TimeSpan.FromSeconds(20), TimeSpan.FromMinutes(10));

    public static readonly CachePolicy Volatile = new(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30));
}