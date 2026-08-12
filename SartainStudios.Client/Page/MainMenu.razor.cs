using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace SartainStudios.Client.Page;

public sealed partial class MainMenu(NavigationManager navigationManager) : ComponentBase
{
    private static bool _hasEvaluatedStartupRedirect;

    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

    protected override async Task OnInitializedAsync()
    {
        if (_hasEvaluatedStartupRedirect) return;
        _hasEvaluatedStartupRedirect = true;

        if (AuthenticationStateTask is null || WasRequestedExplicitly()) return;

        var authenticationState = await AuthenticationStateTask;
        if (authenticationState.User.Identity?.IsAuthenticated is not true) return;

        navigationManager.NavigateTo(Metadata.Invoicing.DashboardRoute, false, true);
    }

    private bool WasRequestedExplicitly()
    {
        var query = navigationManager.ToAbsoluteUri(navigationManager.Uri).Query;
        if (string.IsNullOrWhiteSpace(query)) return false;

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return trimmed
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Any(parts => string.Equals(parts[0], Metadata.MainMenu.StayQueryParameter,
                StringComparison.OrdinalIgnoreCase));
    }
}