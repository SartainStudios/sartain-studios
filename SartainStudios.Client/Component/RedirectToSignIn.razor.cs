using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace SartainStudios.Client.Component;

public sealed partial class RedirectToSignIn(
    NavigationManager navigationManager) : ComponentBase
{
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected override void OnInitialized()
    {
        var returnUrl = Uri.EscapeDataString(new Uri(navigationManager.Uri).PathAndQuery);
        navigationManager.NavigateTo($"sign-in?returnUrl={returnUrl}", replace: true);
    }
}