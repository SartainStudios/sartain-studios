using CorsSettings = SartainStudios.Api.Schema.AppSettings.Cors;

namespace SartainStudios.Api.ServiceCollection;

public static class Cors
{
    public const string ClientPolicy = "SartainStudiosClient";

    public static void AddCors(this IServiceCollection services, IConfiguration configuration)
    {
        var corsSettings = configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>() ?? new CorsSettings();
        var allowedOrigins = corsSettings.AllowedOrigins;
        services.AddCors(options =>
        {
            options.AddPolicy(ClientPolicy, policy =>
                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod());
        });
    }
}