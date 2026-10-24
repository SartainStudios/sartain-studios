using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Component;

public sealed partial class RedirectToSignIn(
    NavigationManager navigationManager) : ComponentBase
{
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected override void OnInitialized()
    {
        var pathAndQuery = new Uri(navigationManager.Uri).PathAndQuery;
        var returnUrl = Uri.EscapeDataString(pathAndQuery);
        var isGigPage =
            pathAndQuery.StartsWith(Metadata.GigEarnings.CurrentSessionRoute, StringComparison.OrdinalIgnoreCase);
        var signInRoute = isGigPage ? Metadata.GigEarnings.SignInRoute.TrimStart('/') : "sign-in";
        navigationManager.NavigateTo($"{signInRoute}?returnUrl={returnUrl}", replace: true);
    }
}