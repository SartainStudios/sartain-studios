using Microsoft.AspNetCore.Components;
using MudBlazor;
using ClientSummary = SartainStudios.Schema.Client.Summary;

namespace SartainStudios.Client.Component;

public sealed partial class ProjectCreateDialog
{
    private static readonly string[] Statuses = ["Active", "Archived"];

    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private bool _wasVisible;
    private MudForm CreateForm { get; set; } = null!;
    private string NewClientId { get; set; } = string.Empty;
    private string NewName { get; set; } = string.Empty;
    private string NewDescription { get; set; } = string.Empty;
    private string NewStatus { get; set; } = "Active";
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public IReadOnlyList<ClientSummary> Clients { get; set; } = [];
    [Parameter] public EventCallback<CreateInput> OnCreate { get; set; }

    protected override void OnParametersSet()
    {
        if (Visible && !_wasVisible)
            ResetFields();
        _wasVisible = Visible;
    }

    private void ResetFields()
    {
        NewClientId = Clients.Count > 0 ? Clients[0].Id : string.Empty;
        NewName = string.Empty;
        NewDescription = string.Empty;
        NewStatus = "Active";
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
        await OnCreate.InvokeAsync(new CreateInput(NewClientId, NewName, NewDescription, NewStatus));
    }

    public sealed record CreateInput(
        string ClientId,
        string Name,
        string Description,
        string Status);
}