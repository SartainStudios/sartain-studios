using SartainStudios.Api.Schema.Notification;

namespace SartainStudios.Api.Service.Notification;

public interface IEmail
{
    void SendEmail(EmailRequest request);
}