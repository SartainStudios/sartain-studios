using SartainStudios.Client.Configuration;
using SartainStudios.Client.Service;
using SartainStudios.Client.Service.Authentication;

namespace SartainStudios.Client.ServiceCollection;

public static class ApiClient
{
    extension(IServiceCollection services)
    {
        public void AddApiClients(IConfiguration configuration, string hostBaseAddress)
        {
            var apiSettings = configuration.GetSection(ApiSettings.SectionName).Get<ApiSettings>()
                              ?? throw new InvalidOperationException("API settings are required.");
            if (string.IsNullOrWhiteSpace(apiSettings.BaseUrl))
                throw new InvalidOperationException(
                    $"{ApiSettings.SectionName}:{nameof(ApiSettings.BaseUrl)} configuration value is required.");
            services.AddTransient<FallbackApiHandler>(sp => new FallbackApiHandler(apiSettings));
            services.AddHttpClient(
                    BearerTokenHandler.RefreshClientName,
                    client => client.BaseAddress = new Uri(apiSettings.BaseUrl))
                .AddHttpMessageHandler<FallbackApiHandler>();
            services.AddApiClient<Authentication>(apiSettings);
            services.AddApiClient<Account>(apiSettings);
            services.AddApiClient<UserAdministration>(apiSettings);
            services.AddApiClient<Organization>(apiSettings);
            services.AddApiClient<Membership>(apiSettings);
            services.AddApiClient<Project>(apiSettings);
            services.AddApiClient<Client.Service.Client>(apiSettings);
            services.AddApiClient<BillingContract>(apiSettings);
            services.AddApiClient<WorkSession>(apiSettings);
            services.AddApiClient<Invoice>(apiSettings);
            services.AddApiClient<OnboardingStatus>(apiSettings);
            services.AddApiClient<Health>(apiSettings);
            services.AddApiClient<GigSession>(apiSettings);
            services.AddApiClient<GigSource>(apiSettings);
            services.AddApiClient<GigReport>(apiSettings);
            services.AddApiClient<GigSettings>(apiSettings);
            services.AddHttpClient<BuildInfoService>(client => client.BaseAddress = new Uri(hostBaseAddress));
        }

        private void AddApiClient<TClient>(ApiSettings apiSettings) where TClient : class
        {
            services.AddHttpClient<TClient>(client => client.BaseAddress = new Uri(apiSettings.BaseUrl))
                .AddHttpMessageHandler<FallbackApiHandler>()
                .AddHttpMessageHandler<BearerTokenHandler>();
        }
    }
}