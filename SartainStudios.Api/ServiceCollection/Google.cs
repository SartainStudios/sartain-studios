using SartainStudios.Api.Authentication;
using SartainStudios.Api.Service.Authentication;
using GoogleSettings = SartainStudios.Api.Schema.AppSettings.Google;

namespace SartainStudios.Api.ServiceCollection;

public static class Google
{
    public static void AddGoogle(this IServiceCollection services, IConfiguration configuration)
    {
        var googleSettings = configuration.GetSection(GoogleSettings.SectionName).Get<GoogleSettings>()
                             ?? throw new InvalidOperationException("Google authentication settings are required.");
        if (string.IsNullOrWhiteSpace(googleSettings.ClientId))
            throw new InvalidOperationException(
                $"{GoogleSettings.SectionName}:{nameof(GoogleSettings.ClientId)} is required.");
        services.AddSingleton(googleSettings);
        services.AddSingleton<IGoogleIdentityValidator, GoogleIdentityValidator>();
    }
}