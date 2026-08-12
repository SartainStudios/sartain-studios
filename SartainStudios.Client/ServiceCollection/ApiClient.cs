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
            var apiBaseUrl = apiSettings.BaseUrl;
            services.AddHttpClient(BearerTokenHandler.RefreshClientName,
                client => client.BaseAddress = new Uri(apiBaseUrl));
            services.AddApiClient<Authentication>(apiBaseUrl);
            services.AddApiClient<Account>(apiBaseUrl);
            services.AddApiClient<Organization>(apiBaseUrl);
            services.AddApiClient<Membership>(apiBaseUrl);
            services.AddApiClient<Project>(apiBaseUrl);
            services.AddApiClient<Client.Service.Client>(apiBaseUrl);
            services.AddApiClient<BillingContract>(apiBaseUrl);
            services.AddApiClient<WorkSession>(apiBaseUrl);
            services.AddApiClient<Invoice>(apiBaseUrl);
            services.AddApiClient<Health>(apiBaseUrl);
            services.AddHttpClient<BuildInfoService>(client => client.BaseAddress = new Uri(hostBaseAddress));
        }

        private void AddApiClient<TClient>(string apiBaseUrl) where TClient : class
        {
            services.AddHttpClient<TClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
                .AddHttpMessageHandler<BearerTokenHandler>();
        }
    }
}