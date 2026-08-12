using MongoDB.Driver;
using SartainStudios.Api.Service.Data;

namespace SartainStudios.Api.ServiceCollection;

public static class Mongo
{
    public static void AddMongo(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings =
            configuration.GetSection(Schema.AppSettings.Mongo.SectionName).Get<Schema.AppSettings.Mongo>()
            ?? throw new InvalidOperationException("MongoDB settings not found in configuration");
        if (string.IsNullOrWhiteSpace(mongoSettings.ConnectionUri))
            throw new InvalidOperationException("MongoDB ConnectionUri is required");
        if (string.IsNullOrWhiteSpace(mongoSettings.DatabaseName))
            throw new InvalidOperationException("MongoDB DatabaseName is required");
        services.Configure<Schema.AppSettings.Mongo>(configuration.GetSection(Schema.AppSettings.Mongo.SectionName));
        var mongoClient = new MongoClient(mongoSettings.ConnectionUri);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddSingleton(mongoSettings);
        services.AddSingleton<Database>();
        services.AddSingleton<IIndexInitializer, IndexInitializer>();
    }
}