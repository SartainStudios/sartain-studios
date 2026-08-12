using SartainStudios.Client.Layout;

namespace SartainStudios.Client.ServiceCollection;

public static class Service
{
    extension(IServiceCollection services)
    {
        public void AddServices()
        {
            services.AddScoped<PageTitleState>();
        }
    }
}