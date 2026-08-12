using SartainStudios.Api.Schema.Notification;

namespace SartainStudios.Api.Service.Notification;

public static class PasswordResetEmail
{
    public const string Subject = "Reset your Sartain Studios password";

    public static string BuildResetLink(string clientBaseUrl, string resetToken)
    {
        return $"{clientBaseUrl.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(resetToken)}";
    }

    public static EmailRequest Build(string recipient, string replyToAddress, string resetLink)
    {
        var body = $"""
                    Hello,
                    We received a request to reset the password for your Sartain Studios account.
                    Reset your password using the link below. This link will expire in 1 hour:
                    {resetLink}
                    If you did not request this, you can safely ignore this email.
                    """;
        var htmlBody = $"""
                        <p>Hello,</p>
                        <p>We received a request to reset the password for your Sartain Studios account.</p>
                        <p><a href="{resetLink}">Reset your password</a> (this link will expire in 1 hour).</p>
                        <p>If you did not request this, you can safely ignore this email.</p>
                        """;
        return new EmailRequest([recipient], [], replyToAddress, Subject, body, null!, htmlBody);
    }
}