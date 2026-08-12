namespace SartainStudios.Client.Page;

public partial class SoftwareRequest
{
    private const string RequestEmail = "request@sartainstudios.com";
    private const string RequestSubject = "Request for a new application or website";
    private static readonly string[] ProjectTypeOptions = ["Application", "Website", "Both", "Other"];
    private string ProjectType { get; set; } = string.Empty;
    private string BusinessName { get; set; } = string.Empty;
    private string Goals { get; set; } = string.Empty;
    private string Timeline { get; set; } = string.Empty;
    private string Budget { get; set; } = string.Empty;

    private string MailTo
    {
        get
        {
            var bodyLines = new[]
            {
                "Hi Sartain Studios LLC,",
                "",
                "I'd like to request a new project.",
                "",
                $"Project type: {ProjectType}",
                $"Business name: {BusinessName}",
                $"What it should do: {Goals}",
                $"Timeline: {Timeline}",
                $"Budget range: {Budget}",
                "",
                "Thanks!"
            };
            return BuildMailTo(RequestEmail, RequestSubject, string.Join(Environment.NewLine, bodyLines));
        }
    }

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