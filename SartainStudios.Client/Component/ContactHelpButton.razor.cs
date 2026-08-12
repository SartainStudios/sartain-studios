using Microsoft.AspNetCore.Components;

namespace SartainStudios.Client.Component;

public partial class ContactHelpButton : ComponentBase
{
    private const string SupportEmail = "help@sartainstudios.com";
    private const string SupportSubject = "Help and support request";

    private static readonly string[] SupportBodyLines =
    [
        "Hi Sartain Studios LLC,",
        "",
        "I need help with the following:",
        "",
        "What happened:",
        "What I expected:",
        "Page or feature:",
        "",
        "Thanks!"
    ];

    private static string SupportMailTo => BuildMailTo(SupportEmail, SupportSubject,
        string.Join(Environment.NewLine, SupportBodyLines));

    private static string BuildMailTo(string to, string subject, string body)
    {
        var parameters = new Dictionary<string, string>
        {
            ["subject"] = subject,
            ["body"] = body
        };
        var query = string.Join("&",
            parameters.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
        return $"mailto:{to}?{query}";
    }
}