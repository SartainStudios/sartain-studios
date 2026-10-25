using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Schema.User;

namespace SartainStudios.Client.Component;

public sealed partial class UserEditDialog
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private bool _wasVisible;
    private string DisplayName { get; set; } = string.Empty;
    private string ProfilePhotoUrl { get; set; } = string.Empty;
    private bool IsAdministrator { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public AdministratorUserResponse? User { get; set; }
    [Parameter] public bool IsSelf { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public string? ErrorMessage { get; set; }
    [Parameter] public EventCallback<AdministratorUpdateUserRequest> OnSave { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible && User is not null)
        {
            DisplayName = User.DisplayName;
            ProfilePhotoUrl = User.ProfilePhotoUrl ?? string.Empty;
            IsAdministrator = User.IsAdministrator;
        }

        _wasVisible = Visible;
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        return VisibleChanged.InvokeAsync(visible);
    }

    private Task CloseAsync()
    {
        return VisibleChanged.InvokeAsync(false);
    }

    private Task SaveAsync()
    {
        return OnSave.HasDelegate
            ? OnSave.InvokeAsync(new AdministratorUpdateUserRequest(
                DisplayName,
                string.IsNullOrWhiteSpace(ProfilePhotoUrl) ? null : ProfilePhotoUrl,
                IsAdministrator))
            : Task.CompletedTask;
    }
}