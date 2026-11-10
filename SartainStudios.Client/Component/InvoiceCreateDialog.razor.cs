using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Schema.Invoice;
using BillingContractService = SartainStudios.Client.Service.BillingContract;
using BillingSummary = SartainStudios.Schema.Billing.Summary;
using InvoiceService = SartainStudios.Client.Service.Invoice;

namespace SartainStudios.Client.Component;

public sealed partial class InvoiceCreateDialog(
    BillingContractService billingContractService,
    InvoiceService invoiceService,
    ISnackbar snackbar)
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Large,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };
    private static readonly TimeSpan AutoSelectGapThreshold = TimeSpan.FromHours(24);
    private bool _wasVisible;
    private int _loadVersion;
    private List<BillingSummary>? Contracts { get; set; }
    private List<SelectableSession>? Sessions { get; set; }
    private HashSet<string> SelectedSessionIds { get; set; } = [];
    private string SelectedContractId { get; set; } = string.Empty;
    private DateTime? DueDate { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoadingSessions { get; set; }

    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<CreateRequest> OnCreate { get; set; }

    private int SelectedTotalMinutes =>
        Sessions?.Where(s => SelectedSessionIds.Contains(s.SessionId)).Sum(s => s.MinutesWorked) ?? 0;

    private decimal EstimatedTotal
    {
        get
        {
            var contract = Contracts?.FirstOrDefault(c => c.Id == SelectedContractId);
            return contract is null
                ? 0m
                : Math.Round(contract.HourlyRate * SelectedTotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var opening = Visible && !_wasVisible;
        if (Visible != _wasVisible)
            _loadVersion++;
        _wasVisible = Visible;
        if (!opening) return;

        Contracts = null;
        Sessions = null;
        SelectedSessionIds.Clear();
        SelectedContractId = string.Empty;
        ErrorMessage = null;
        IsLoadingSessions = false;
        var today = DateTime.Today;
        var daysUntilMonday = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
        DueDate = today.AddDays((daysUntilMonday == 0 ? 7 : daysUntilMonday) + 7);
        var version = _loadVersion;
        try
        {
            var contracts = await billingContractService.ListAsync();
            if (version != _loadVersion) return;
            Contracts = contracts.Where(c => c.IsActive).ToList();
            if (Contracts.Count == 1)
            {
                SelectedContractId = Contracts[0].Id;
                StateHasChanged();
                await LoadSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Contracts = [];
        }
    }

    private void OnContractChanged(string value)
    {
        if (SelectedContractId == value) return;
        _loadVersion++;
        SelectedContractId = value;
        Sessions = null;
        SelectedSessionIds.Clear();
        IsLoadingSessions = false;
        ErrorMessage = null;
    }

    private async Task LoadSessionsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedContractId)) return;
        var version = ++_loadVersion;
        var contractId = SelectedContractId;
        ErrorMessage = null;
        IsLoadingSessions = true;
        Sessions = null;
        SelectedSessionIds.Clear();
        try
        {
            var sessions = await invoiceService.GetSelectableSessionsAsync(contractId);
            if (version != _loadVersion) return;
            Sessions = sessions.ToList();
            for (var i = 0; i < Sessions.Count; i++)
            {
                SelectedSessionIds.Add(Sessions[i].SessionId);
                if (i == Sessions.Count - 1 ||
                    Sessions[i + 1].StartTime - Sessions[i].EndTime > AutoSelectGapThreshold) break;
            }
        }
        catch (Exception ex)
        {
            if (version != _loadVersion) return;
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Sessions = [];
        }
        finally
        {
            if (version == _loadVersion)
                IsLoadingSessions = false;
        }
    }

    private Task OnVisibleChangedAsync(bool visible) => VisibleChanged.InvokeAsync(visible);
    private Task CloseAsync() => VisibleChanged.InvokeAsync(false);

    private Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedContractId) || SelectedSessionIds.Count == 0 || DueDate is null)
            return Task.CompletedTask;
        var dueDateUtc = DateTime.SpecifyKind(DueDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
        return OnCreate.InvokeAsync(new CreateRequest(SelectedContractId, SelectedSessionIds.ToList(), dueDateUtc));
    }

    private void ToggleSession(string sessionId, bool selected)
    {
        if (selected) SelectedSessionIds.Add(sessionId);
        else SelectedSessionIds.Remove(sessionId);
    }

    private void SelectAll()
    {
        if (Sessions is not null) SelectedSessionIds = Sessions.Select(s => s.SessionId).ToHashSet();
    }

    private void ClearSelection() => SelectedSessionIds.Clear();

    private static string FormatDuration(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }
}
