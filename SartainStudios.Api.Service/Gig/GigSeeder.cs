using MongoDB.Driver;
using SartainStudios.Api.Service.Data;
using SartainStudios.Schema.DatabaseEntity;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Api.Service.Gig;

public sealed class GigSeeder(Database database, TimeProvider timeProvider)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        await SeedAsync(database.GigSources, GigDefaults.Sources, now, cancellationToken);
        await SeedAsync(database.GigExpenseCategories, GigDefaults.Categories, now, cancellationToken);
    }

    private static async Task SeedAsync<T>(
        IMongoCollection<T> collection,
        IReadOnlyList<(string Name, string Color)> defaults,
        DateTime now,
        CancellationToken cancellationToken) where T : GigOption, new()
    {
        var existing = await collection
            .Find(option => option.IsDefault)
            .ToListAsync(cancellationToken);
        var names = existing.Select(option => option.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, color) in defaults)
        {
            if (names.Contains(name)) continue;
            try
            {
                await collection.InsertOneAsync(
                    new T
                    {
                        UserId = null, Name = name, Color = color, IsDefault = true, CreatedAt = now, UpdatedAt = now
                    },
                    cancellationToken: cancellationToken);
            }
            catch (MongoWriteException exception) when (exception.WriteError.Category ==
                                                        ServerErrorCategory.DuplicateKey)
            {
                // Another instance seeded this default at the same moment.
            }
        }
    }
}