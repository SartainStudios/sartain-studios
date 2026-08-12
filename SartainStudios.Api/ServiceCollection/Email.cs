using SartainStudios.Api.Service.Notification;
using EmailSettings = SartainStudios.Api.Schema.AppSettings.Email;

namespace SartainStudios.Api.ServiceCollection;

public static class Email
{
    public static void AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var emailSettings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>()
                            ?? throw new InvalidOperationException("Email settings are required.");
        if (string.IsNullOrWhiteSpace(emailSettings.Host))
            throw new InvalidOperationException(
                $"{EmailSettings.SectionName}:{nameof(EmailSettings.Host)} is required.");
        if (string.IsNullOrWhiteSpace(emailSettings.Username))
            throw new InvalidOperationException(
                $"{EmailSettings.SectionName}:{nameof(EmailSettings.Username)} is required.");
        if (string.IsNullOrWhiteSpace(emailSettings.Password))
            throw new InvalidOperationException(
                $"{EmailSettings.SectionName}:{nameof(EmailSettings.Password)} is required.");
        if (string.IsNullOrWhiteSpace(emailSettings.Sender))
            throw new InvalidOperationException(
                $"{EmailSettings.SectionName}:{nameof(EmailSettings.Sender)} is required.");
        services.AddSingleton(emailSettings);
        services.AddSingleton<IEmail, Service.Notification.Email>();
    }
}