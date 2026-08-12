using SartainStudios.Api.Service.Notification;
using MonitorSettings = SartainStudios.Api.Schema.AppSettings.HourLimitMonitor;

namespace SartainStudios.Api.ServiceCollection;

public static class HourLimitMonitor
{
    public static void AddHourLimitMonitor(this IServiceCollection services, IConfiguration configuration)
    {
        var monitorSettings = configuration.GetSection(MonitorSettings.SectionName).Get<MonitorSettings>()
                              ?? new MonitorSettings();
        services.AddSingleton(monitorSettings);
        services.AddHostedService<HourLimitMonitorService>();
    }
}