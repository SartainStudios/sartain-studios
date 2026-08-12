using System.Reflection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SartainStudios.Api.Schema.AppSettings;
using SartainStudios.Api.Schema.Notification;
using SartainStudios.Api.Service.Notification;
using SartainStudios.Api.Service.Test.Infrastructure;
using SartainStudios.Schema.Invoice;
using SartainStudios.Schema.WorkSession;
using ClientSnapshot = SartainStudios.Schema.Client.Snapshot;
using EmailService = SartainStudios.Api.Service.Notification.Email;
using EmailSettings = SartainStudios.Api.Schema.AppSettings.Email;
using InvoiceEntity = SartainStudios.Schema.DatabaseEntity.Invoice;
using OrganizationSnapshot = SartainStudios.Schema.Organization.Snapshot;
using ProjectSnapshot = SartainStudios.Schema.Project.Snapshot;

namespace SartainStudios.Api.Service.Test.Notification;

public sealed class NotificationTests
{
    [Fact]
    public void PasswordResetEmail_BuildResetLink_EscapesToken()
    {
        var result = PasswordResetEmail.BuildResetLink("https://example.com/", "a b&c");

        Assert.Equal("https://example.com/reset-password?token=a%20b%26c", result);
    }

    [Fact]
    public void PasswordResetEmail_Build_CreatesExpectedRequest()
    {
        var request = PasswordResetEmail.Build("user@example.com", "reply@example.com", "https://example.com/reset");

        Assert.Equal(new[] { "user@example.com" }, request.Recipients);
        Assert.Empty(request.CcRecipients);
        Assert.Equal("reply@example.com", request.ReplyToAddress);
        Assert.Equal(PasswordResetEmail.Subject, request.Subject);
        Assert.Contains("https://example.com/reset", request.Body);
        Assert.Contains("href=\"https://example.com/reset\"", request.HtmlBody);
    }

    [Fact]
    public void InvoiceEmail_Build_CreatesExpectedRequest()
    {
        var invoice = new InvoiceEntity
        {
            InvoiceNumber = "INV-0001",
            DueDate = new DateTime(2026, 8, 11),
            TotalAmount = 123.45m,
            TotalMinutesWorked = 120,
            OrganizationSnapshot = new OrganizationSnapshot
            {
                Name = "Sartain Studios",
                Email = "billing@sartainstudios.com"
            },
            ClientSnapshot = new ClientSnapshot
            {
                ContactPerson = "Jane Doe",
                Email = "jane@example.com"
            }
        };
        var detail = new Detail(
            "1",
            "org-1",
            "client-1",
            "INV-0001",
            new OrganizationSnapshot { Name = "Sartain Studios", Email = "billing@sartainstudios.com" },
            new ClientSnapshot { ContactPerson = "Jane Doe", Email = "jane@example.com" },
            new ProjectSnapshot(),
            new DateTime(2026, 8, 11),
            123.45m,
            120,
            1,
            123.45m,
            "Sent",
            Array.Empty<string>(),
            new[] { new DailyBreakdownEntry(new DateOnly(2026, 8, 11), 120, 123.45m) },
            DateTime.UtcNow,
            DateTime.UtcNow);

        var request = InvoiceEmail.Build(invoice, detail, new byte[] { 1, 2, 3 });

        Assert.Equal(new[] { "jane@example.com" }, request.Recipients);
        Assert.Equal(new[] { "billing@sartainstudios.com" }, request.CcRecipients);
        Assert.Equal("billing@sartainstudios.com", request.ReplyToAddress);
        Assert.Equal("Invoice INV-0001 from Sartain Studios", request.Subject);
        Assert.Equal("INV-0001.pdf", request.Attachment.FileName);
        Assert.Equal("application/pdf", request.Attachment.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, request.Attachment.Content);
        Assert.Contains("Hello Jane Doe", request.Body);
        Assert.Contains("08/11/2026", request.Body);
        Assert.Contains("$123.45", request.Body);
        Assert.Contains("Hello Jane Doe", request.HtmlBody);
        Assert.Contains("Sartain Studios", request.HtmlBody);
    }

    [Fact]
    public void Email_SendEmail_WithInvalidRecipientThrows()
    {
        var settings = new EmailSettings
        {
            Host = "localhost",
            Port = 25,
            Username = "user",
            Password = "pass",
            Sender = "sender@example.com"
        };
        var email = new EmailService(settings);
        var request = new EmailRequest(new[] { "not-an-email" }, Array.Empty<string>(), "reply@example.com", "Subject",
            "Body", null!);

        Assert.Throws<FormatException>(() => email.SendEmail(request));
    }

    [Fact]
    public void HourLimitMonitorService_SendEmail_UsesExpectedMessage()
    {
        var harness = new MongoHarness();
        var email = Substitute.For<IEmail>();
        var settings = new EmailSettings
        {
            Host = "localhost",
            Port = 25,
            Username = "user",
            Password = "pass",
            Sender = "sender@example.com"
        };
        var monitorSettings = new HourLimitMonitor { PollIntervalSeconds = 30 };
        var logger = Substitute.For<ILogger<HourLimitMonitorService>>();
        var service = new HourLimitMonitorService(harness.Database, email, settings, monitorSettings, logger);

        InvokePrivate(service, "SendEmail", "member@example.com", HourLimitNotificationType.Reached, 150, 240);

        email.Received(1).SendEmail(Arg.Is<EmailRequest>(request =>
            request.Recipients.Length == 1 &&
            request.Recipients[0] == "member@example.com" &&
            request.ReplyToAddress == "sender@example.com" &&
            request.Subject == "You've reached your weekly hour limit" &&
            request.Body == "You've logged 2.5 hours this week, reaching your 4-hour weekly limit."));
    }

    [Fact]
    public void HourLimitMonitorService_GetWeekStart_ReturnsMonday()
    {
        var result =
            InvokePrivateStatic<DateTime>(typeof(HourLimitMonitorService), "GetWeekStart", new DateTime(2026, 8, 11));

        Assert.Equal(new DateTime(2026, 8, 10), result);
    }

    private static void InvokePrivate(object instance, string methodName, params object?[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        method.Invoke(instance, arguments);
    }

    private static T InvokePrivateStatic<T>(Type type, string methodName, params object?[] arguments)
    {
        var method = type.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var result = method.Invoke(null, arguments);
        Assert.NotNull(result);
        return (T)result;
    }
}