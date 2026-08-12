using ClientSettings = SartainStudios.Api.Schema.AppSettings.ClientSettings;

namespace SartainStudios.Api.ServiceCollection;

public static class Client
{
    public static void AddClientSettings(this IServiceCollection services, IConfiguration configuration)
    {
        var clientSettings = configuration.GetSection(ClientSettings.SectionName).Get<ClientSettings>()
                             ?? throw new InvalidOperationException("ClientSettings are required.");
        if (string.IsNullOrWhiteSpace(clientSettings.BaseUrl))
            throw new InvalidOperationException(
                $"{ClientSettings.SectionName}:{nameof(ClientSettings.BaseUrl)} is required.");
        services.AddSingleton(clientSettings);
    }
}