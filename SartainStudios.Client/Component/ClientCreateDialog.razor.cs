using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Schema;

namespace SartainStudios.Client.Component;

public sealed partial class ClientCreateDialog
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
    private string NewCompanyName { get; set; } = string.Empty;
    private string NewContactPerson { get; set; } = string.Empty;
    private Address NewAddress { get; set; } = new();
    private string NewEmail { get; set; } = string.Empty;
    private string NewPhoneNumber { get; set; } = string.Empty;
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<CreateInput> OnCreate { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
            ResetFields();
        _wasVisible = Visible;
    }

    private void ResetFields()
    {
        NewCompanyName = string.Empty;
        NewContactPerson = string.Empty;
        NewAddress = new Address();
        NewEmail = string.Empty;
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
        await OnCreate.InvokeAsync(new CreateInput(
            NewCompanyName,
            NewContactPerson,
            NewAddress,
            NewEmail,
            NewPhoneNumber));
    }

    public sealed record CreateInput(
        string CompanyName,
        string ContactPerson,
        Address Address,
        string Email,
        string PhoneNumber);
}