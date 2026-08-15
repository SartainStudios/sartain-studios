namespace SartainStudios.Client.Service;

internal static class UserTimeZone
{
    internal static string Append(string url)
    {
        var timeZoneId = Uri.EscapeDataString(TimeZoneInfo.Local.Id);

        var separator = url.Contains('?') ? "&" : "?";

        return $"{url}{separator}userTimeZoneId={timeZoneId}";
    }
}