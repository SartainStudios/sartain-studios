using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
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
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar) : ComponentBase, IAsyncDisposable
{
    private const string GigModeName = "gig";
    private DotNetObjectReference<GoogleSignInButton>? _selfReference;
    [Parameter] public string RedirectUrl { get; set; } = "/";
    [Parameter] public string? OrganizationName { get; set; }

    [Parameter] public bool GigMode { get; set; }

    [Parameter] public EventCallback<Response> OnSignedIn { get; set; }
    [Parameter] public EventCallback<bool> OnBusyChanged { get; set; }
    private string ElementId { get; } = $"google-signin-{Guid.NewGuid():N}";
    private string? ErrorMessage { get; set; }
    private bool UseRedirectFlow { get; set; }
    private bool IsBusy { get; set; }
    private string BusyMessage { get; set; } = "Signing you in...";

    public async ValueTask DisposeAsync()
    {
        _selfReference?.Dispose();
        await Task.CompletedTask;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (await jsRuntime.InvokeAsync<bool>("googleAuthentication.hasRedirectResult"))
                await SetBusyAsync(true, "Finishing sign in with Google...");
        }
        catch (JSException)
        {
            // Ignore: the helper script is unavailable.
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ErrorMessage = "Google sign-in is not configured.";
            await SetBusyAsync(false);
            StateHasChanged();
            return;
        }

        if (await TryCompleteRedirectAsync()) return;

        UseRedirectFlow = await jsRuntime.InvokeAsync<bool>("googleAuthentication.shouldUseRedirect");
        StateHasChanged();
        if (UseRedirectFlow) return;

        _selfReference = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("googleAuthentication.render", ElementId, clientId, _selfReference);
    }

    [JSInvokable]
    public Task OnGoogleCredential(string credential)
    {
        return CompleteSignInAsync(credential, null, GigMode);
    }

    [JSInvokable]
    public async Task OnGoogleScriptUnavailable()
    {
        UseRedirectFlow = true;
        StateHasChanged();
        await Task.CompletedTask;
    }

    private async Task StartRedirectAsync()
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ErrorMessage = "Google sign-in is not configured.";
            StateHasChanged();
            return;
        }

        ErrorMessage = null;
        await SetBusyAsync(true, "Redirecting to Google...");
        await jsRuntime.InvokeVoidAsync("googleAuthentication.signInWithRedirect", clientId, ResolveTarget(null),
            GigMode ? GigModeName : string.Empty);
    }

    private async Task<bool> TryCompleteRedirectAsync()
    {
        var result = await jsRuntime.InvokeAsync<RedirectResult?>("googleAuthentication.consumeRedirectResult");
        if (result is null) return false;

        if (!string.IsNullOrWhiteSpace(result.Error))
        {
            ErrorMessage = result.Error;
            snackbar.Add(result.Error, Severity.Error);
            await SetBusyAsync(false);
            StateHasChanged();
            return false;
        }

        if (string.IsNullOrWhiteSpace(result.Credential))
        {
            await SetBusyAsync(false);
            return false;
        }

        await CompleteSignInAsync(result.Credential, result.ReturnUrl, result.Mode == GigModeName);
        return true;
    }

    private async Task CompleteSignInAsync(string credential, string? returnUrl, bool gigSignIn)
    {
        ErrorMessage = null;
        await SetBusyAsync(true, "Signing you in...");
        try
        {
            if (gigSignIn)
            {
                await authentication.GigGoogleSignInAsync(new GigGoogleSignInRequest(credential));
                await authenticationStateProvider.GetAuthenticationStateAsync();
                BusyMessage = "Taking you to your current session...";
                StateHasChanged();
                navigationManager.NavigateTo(ResolveTarget(returnUrl, true), false, true);
                return;
            }

            var response = await authentication.GoogleSignInAsync(new GoogleSignInRequest(
                credential,
                string.IsNullOrWhiteSpace(OrganizationName) ? null : OrganizationName,
                null,
                null,
                null));

            await authenticationStateProvider.GetAuthenticationStateAsync();

            if (OnSignedIn.HasDelegate)
            {
                await SetBusyAsync(false);
                await OnSignedIn.InvokeAsync(response);
                return;
            }

            BusyMessage = "Taking you to your dashboard...";
            StateHasChanged();
            navigationManager.NavigateTo(ResolveTarget(returnUrl), false, true);
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            snackbar.Add(exception.Message, Severity.Error);
            await SetBusyAsync(false);
            StateHasChanged();
        }
    }

    private string ResolveTarget(string? returnUrl, bool gigSignIn = false)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)) return returnUrl;
        if (string.IsNullOrWhiteSpace(RedirectUrl) || (gigSignIn && !GigMode))
            return gigSignIn ? Metadata.GigEarnings.CurrentSessionRoute : Metadata.MainMenu.IndexRoute;
        return RedirectUrl;
    }

    private async Task SetBusyAsync(bool isBusy, string? message = null)
    {
        if (message is not null) BusyMessage = message;
        if (IsBusy == isBusy) return;
        IsBusy = isBusy;
        StateHasChanged();
        if (OnBusyChanged.HasDelegate) await OnBusyChanged.InvokeAsync(isBusy);
    }

    public sealed record RedirectResult(string? Credential, string? ReturnUrl, string? Error, string? Mode);
}