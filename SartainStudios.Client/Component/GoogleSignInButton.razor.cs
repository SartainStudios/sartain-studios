using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Authentication;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Component;

public sealed partial class GoogleSignInButton(
    IJSRuntime jsRuntime,
    IConfiguration configuration,
    Authentication authentication,
    NavigationManager navigationManager,
    ISnackbar snackbar) : ComponentBase, IAsyncDisposable
{
    private DotNetObjectReference<GoogleSignInButton>? _selfReference;
    [Parameter] public string RedirectUrl { get; set; } = "/";
    [Parameter] public string? OrganizationName { get; set; }
    [Parameter] public EventCallback<Response> OnSignedIn { get; set; }
    private string ElementId { get; } = $"google-signin-{Guid.NewGuid():N}";
    private string? ErrorMessage { get; set; }

    public async ValueTask DisposeAsync()
    {
        _selfReference?.Dispose();
        await Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ErrorMessage = "Google sign-in is not configured.";
            StateHasChanged();
            return;
        }

        _selfReference = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("googleAuthentication.render", ElementId, clientId, _selfReference);
    }

    [JSInvokable]
    public async Task OnGoogleCredential(string credential)
    {
        try
        {
            snackbar.Add("Signing in with Google...", Severity.Info);
            var response = await authentication.GoogleSignInAsync(new GoogleSignInRequest(
                credential,
                string.IsNullOrWhiteSpace(OrganizationName) ? null : OrganizationName,
                null,
                null,
                null));
            snackbar.Add("Signed in successfully.", Severity.Success);
            if (OnSignedIn.HasDelegate)
                await OnSignedIn.InvokeAsync(response);
            else
                navigationManager.NavigateTo(
                    string.IsNullOrWhiteSpace(RedirectUrl)
                        ? Metadata.MainMenu.IndexRoute
                        : RedirectUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            snackbar.Add(ex.Message, Severity.Error);
            StateHasChanged();
        }
    }
}