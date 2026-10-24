using Microsoft.AspNetCore.Components;

namespace SartainStudios.Client.Page;

public sealed partial class GigSignIn(NavigationManager navigationManager)
{
    private string ReturnUrl
    {
        get
        {
            var uri = navigationManager.ToAbsoluteUri(navigationManager.Uri);
            var target = ExtractReturnUrl(uri.Query);

            return !string.IsNullOrWhiteSpace(target)
                   && target.StartsWith(Metadata.GigEarnings.CurrentSessionRoute, StringComparison.OrdinalIgnoreCase)
                   && !target.StartsWith(Metadata.GigEarnings.SignInRoute, StringComparison.OrdinalIgnoreCase)
                ? target
                : Metadata.GigEarnings.CurrentSessionRoute;
        }
    }

    private static string? ExtractReturnUrl(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return (from pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            select pair.Split('=', 2)
            into parts
            where parts.Length == 2 && string.Equals(parts[0], "returnUrl", StringComparison.OrdinalIgnoreCase)
            select Uri.UnescapeDataString(parts[1])).FirstOrDefault();
    }
}