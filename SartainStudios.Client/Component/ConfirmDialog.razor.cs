using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace SartainStudios.Client.Component;

public sealed partial class ConfirmDialog
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.ExtraSmall,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public string Title { get; set; } = "Are you sure?";
    [Parameter] public string Message { get; set; } = string.Empty;
    [Parameter] public string? Details { get; set; }
    [Parameter] public string Icon { get; set; } = Icons.Material.Filled.HelpOutline;
    [Parameter] public string? ConfirmIcon { get; set; }
    [Parameter] public string ConfirmText { get; set; } = "Confirm";
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public Color ConfirmColor { get; set; } = Color.Primary;
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private Task OnVisibleChangedAsync(bool visible)
    {
        return visible ? VisibleChanged.InvokeAsync(true) : CancelAsync();
    }

    private async Task CancelAsync()
    {
        await VisibleChanged.InvokeAsync(false);
        if (OnCancel.HasDelegate)
            await OnCancel.InvokeAsync();
    }

    private Task ConfirmAsync()
    {
        return OnConfirm.HasDelegate ? OnConfirm.InvokeAsync() : Task.CompletedTask;
    }
}