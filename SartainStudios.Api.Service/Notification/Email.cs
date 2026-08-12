using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using SartainStudios.Api.Schema.Notification;
using EmailSettings = SartainStudios.Api.Schema.AppSettings.Email;

namespace SartainStudios.Api.Service.Notification;

public class Email(EmailSettings settings) : IEmail
{
    public void SendEmail(EmailRequest request)
    {
        using var client = new SmtpClient(settings.Host, settings.Port);
        client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        client.EnableSsl = true;
        using var message = new MailMessage(
            settings.Sender,
            string.Join(",", request.Recipients),
            request.Subject,
            request.Body);
        if (!string.IsNullOrWhiteSpace(request.HtmlBody))
        {
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(request.Body, null, MediaTypeNames.Text.Plain));
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(request.HtmlBody, null, MediaTypeNames.Text.Html));
        }

        if (!string.IsNullOrEmpty(request.ReplyToAddress)) message.ReplyToList.Add(request.ReplyToAddress);
        if (request.CcRecipients is { Length: > 0 })
            foreach (var cc in request.CcRecipients)
                if (!string.IsNullOrWhiteSpace(cc))
                    message.CC.Add(cc);
        using var attachmentStream = request.Attachment != null
            ? new MemoryStream(request.Attachment.Content)
            : null;
        if (request.Attachment != null && attachmentStream != null)
            message.Attachments.Add(
                new Attachment(attachmentStream, request.Attachment.FileName, request.Attachment.ContentType));
        client.Send(message);
    }
}