using SartainStudios.Client.Layout;
using SartainStudios.Client.Service;

namespace SartainStudios.Client.ServiceCollection;

public static class Service
{
    extension(IServiceCollection services)
    {
        public void AddServices()
        {
            services.AddScoped<OnboardingStatus>();
            services.AddScoped<PageTitleState>();
        }
    }
}