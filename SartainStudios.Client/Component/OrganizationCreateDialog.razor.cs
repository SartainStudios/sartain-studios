using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Schema;
using SartainStudios.Schema.Organization;

namespace SartainStudios.Client.Component;

public sealed partial class OrganizationCreateDialog
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private bool _wasVisible;
    private MudForm CreateForm { get; set; } = null!;
    private string NewName { get; set; } = string.Empty;
    private string NewEmail { get; set; } = string.Empty;
    private Address NewAddress { get; set; } = new();
    private string NewPhoneNumber { get; set; } = string.Empty;
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<CreateRequest> OnCreate { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
            ResetFields();
        _wasVisible = Visible;
    }

    private void ResetFields()
    {
        NewName = string.Empty;
        NewEmail = string.Empty;
        NewAddress = new Address();
        NewPhoneNumber = string.Empty;
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        return VisibleChanged.InvokeAsync(visible);
    }

    private Task CloseAsync()
    {
        return VisibleChanged.InvokeAsync(false);
    }

    private async Task CreateAsync()
    {
        await CreateForm.ValidateAsync();
        if (!CreateForm.IsValid || !OnCreate.HasDelegate)
            return;
        await OnCreate.InvokeAsync(new CreateRequest(
            NewName,
            NewAddress.HasValue ? NewAddress : null,
            string.IsNullOrWhiteSpace(NewEmail) ? null : NewEmail,
            string.IsNullOrWhiteSpace(NewPhoneNumber) ? null : NewPhoneNumber));
    }
}