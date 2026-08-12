namespace SartainStudios.Api.Schema.Notification;

public sealed class EmailRequest
{
    public readonly EmailAttachment Attachment;
    public readonly string Body;
    public readonly string[] CcRecipients;
    public readonly string? HtmlBody;
    public readonly string[] Recipients;
    public readonly string ReplyToAddress;
    public readonly string Subject;

    public EmailRequest(
        string[] recipients,
        string[] ccRecipients,
        string replyToAddress,
        string subject,
        string body,
        EmailAttachment attachment,
        string? htmlBody = null)
    {
        if (recipients == null || recipients.Length == 0)
            throw new ArgumentException("Recipients cannot be null or empty.");
        if (string.IsNullOrEmpty(replyToAddress))
            throw new ArgumentException("ReplyToAddress cannot be null or empty.");
        if (string.IsNullOrEmpty(subject))
            throw new ArgumentException("Subject cannot be null or empty.");
        if (string.IsNullOrEmpty(body))
            throw new ArgumentException("Body cannot be null or empty.");
        Recipients = recipients;
        CcRecipients = ccRecipients;
        ReplyToAddress = replyToAddress;
        Subject = subject;
        Body = body;
        HtmlBody = htmlBody;
        Attachment = attachment;
    }
}