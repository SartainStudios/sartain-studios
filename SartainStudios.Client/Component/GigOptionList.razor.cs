using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Component;

public sealed partial class GigOptionList
{
    [Parameter] public string Noun { get; set; } = "item";
    [Parameter] public IReadOnlyList<OptionItem> Items { get; set; } = [];
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<OptionRequest> OnCreate { get; set; }
    [Parameter] public EventCallback<(string Id, OptionRequest Request)> OnUpdate { get; set; }
    [Parameter] public EventCallback<OptionItem> OnToggleHidden { get; set; }
    [Parameter] public EventCallback<OptionItem> OnDelete { get; set; }

    private string NewName { get; set; } = string.Empty;
    private string NewColor { get; set; } = GigDefaults.FallbackColor;
    private string? EditingId { get; set; }
    private string EditName { get; set; } = string.Empty;
    private string EditColor { get; set; } = GigDefaults.FallbackColor;

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(NewName)) return;
        await OnCreate.InvokeAsync(new OptionRequest(NewName, NewColor));
        NewName = string.Empty;
    }

    private async Task OnNewKeyUp(KeyboardEventArgs args)
    {
        if (args.Key == "Enter" && !IsBusy) await CreateAsync();
    }

    private void OnNewColorChanged(ChangeEventArgs args)
    {
        NewColor = args.Value?.ToString() ?? GigDefaults.FallbackColor;
    }

    private void OnEditColorChanged(ChangeEventArgs args)
    {
        EditColor = args.Value?.ToString() ?? GigDefaults.FallbackColor;
    }

    private void BeginEdit(OptionItem item)
    {
        EditingId = item.Id;
        EditName = item.Name;
        EditColor = item.Color;
    }

    private void CancelEdit()
    {
        EditingId = null;
    }

    private async Task SaveEditAsync()
    {
        if (EditingId is null || string.IsNullOrWhiteSpace(EditName)) return;
        await OnUpdate.InvokeAsync((EditingId, new OptionRequest(EditName, EditColor)));
        EditingId = null;
    }

    private Task ToggleHiddenAsync(OptionItem item)
    {
        return OnToggleHidden.InvokeAsync(item);
    }

    private Task DeleteAsync(OptionItem item)
    {
        return OnDelete.InvokeAsync(item);
    }

    public sealed record OptionItem(string Id, string Name, string Color, bool IsDefault, bool IsHidden);
}