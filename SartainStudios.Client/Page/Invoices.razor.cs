using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using MudBlazor;
using SartainStudios.Schema.Invoice;
using SartainStudios.Schema.Membership;
using BillingContractService = SartainStudios.Client.Service.BillingContract;
using BillingSummary = SartainStudios.Schema.Billing.Summary;
using CreateRequest = SartainStudios.Schema.Invoice.CreateRequest;
using EditRequest = SartainStudios.Schema.Invoice.EditRequest;
using InvoiceDetail = SartainStudios.Schema.Invoice.Detail;
using InvoiceService = SartainStudios.Client.Service.Invoice;
using Summary = SartainStudios.Schema.Invoice.Summary;
using UpdateRequest = SartainStudios.Schema.Invoice.UpdateRequest;

namespace SartainStudios.Client.Page;

public sealed partial class Invoices(
    InvoiceService invoiceService,
    BillingContractService billingContractService,
    AuthenticationStateProvider authenticationStateProvider,
    IJSRuntime jsRuntime,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private static readonly TimeSpan AutoSelectGapThreshold = TimeSpan.FromHours(24);
    private bool _createRequestHandled;
    private string? _lastId;

    [Parameter]
    [SupplyParameterFromQuery(Name = "id")]
    public string? Id { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "new")]
    public bool CreateRequested { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "edit")]
    public bool EditRequested { get; set; }

    private List<Summary>? InvoiceSummaries { get; set; }
    private List<BillingSummary>? Contracts { get; set; }
    private List<SelectableSession>? SelectableSessions { get; set; }
    private InvoiceDetail? SelectedInvoice { get; set; }
    private HashSet<string> SelectedSessionIds { get; set; } = [];
    private string CurrentRole { get; set; } = string.Empty;
    private string StatusFilter { get; set; } = Lifecycle.AnyStatus;
    private string SelectedContractId { get; set; } = string.Empty;
    private string NewStatus { get; set; } = string.Empty;
    private DateTime? DueDate { get; set; } = CalculateDefaultDueDate(DateTime.Today);
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsLoading { get; set; }
    private bool IsLoadingSessions { get; set; }
    private bool IsDownloading { get; set; }
    private bool IsPreviewing { get; set; }
    private bool IsSending { get; set; }
    private bool IsEditMode { get; set; }
    private IReadOnlyList<string> AllowedTransitions { get; set; } = [];

    private bool CanManage => string.Equals(CurrentRole, nameof(RoleType.Owner), StringComparison.OrdinalIgnoreCase)
                              || string.Equals(CurrentRole, nameof(RoleType.Administrator),
                                  StringComparison.OrdinalIgnoreCase);

    private bool IsDetailView => !string.IsNullOrWhiteSpace(Id);
    private bool IsCreateView => !IsDetailView && CreateRequested && CanManage;

    private BillingSummary? SelectedContract =>
        Contracts?.FirstOrDefault(c => c.Id == SelectedContractId);

    private int SelectedTotalMinutes =>
        SelectableSessions?
            .Where(s => SelectedSessionIds.Contains(s.SessionId))
            .Sum(s => s.MinutesWorked) ?? 0;

    private decimal EstimatedTotal =>
        SelectedContract is null
            ? 0m
            : Math.Round(SelectedContract.HourlyRate * SelectedTotalMinutes / 60m, 2, MidpointRounding.AwayFromZero);

    protected override async Task OnInitializedAsync()
    {
        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        CurrentRole = state.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        _lastId = Id;
        IsEditMode = IsDetailView && EditRequested && CanManage;
        if (IsDetailView)
            await LoadInvoiceAsync();
        else if (IsCreateView)
            await LoadCreateContractsAsync();
        else
            await LoadListAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (!CanManage)
            CreateRequested = false;
        if (CreateRequested && !_createRequestHandled && !IsDetailView && CanManage)
        {
            _createRequestHandled = true;
            await LoadCreateContractsAsync();
        }
        else if (!CreateRequested)
        {
            _createRequestHandled = false;
        }

        var previousEditMode = IsEditMode;
        IsEditMode = IsDetailView && EditRequested && CanManage;
        if (previousEditMode && !IsEditMode)
            ClearEditState();
        if (Id != _lastId)
        {
            _lastId = Id;
            ErrorMessage = null;
            if (IsDetailView)
            {
                await LoadInvoiceAsync();
            }
            else
            {
                SelectedInvoice = null;
                ClearEditState();
                if (IsCreateView)
                    await LoadCreateContractsAsync();
                else
                    await LoadListAsync();
            }
        }

        if (IsDetailView && IsEditMode && SelectedInvoice is not null && SelectableSessions is null)
            await LoadEditableSessionsAsync();
    }

    private async Task LoadListAsync()
    {
        ErrorMessage = null;
        InvoiceSummaries = null;
        try
        {
            var list = await invoiceService.ListAsync(status: StatusFilter);
            InvoiceSummaries = list.ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            InvoiceSummaries = [];
        }
    }

    private async Task LoadInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return;
        IsLoading = true;
        ErrorMessage = null;
        ClearEditState();
        try
        {
            SelectedInvoice = await invoiceService.GetAsync(Id);
            AllowedTransitions = Lifecycle.AllowedTransitionsFrom(SelectedInvoice.Status);
            NewStatus = AllowedTransitions.FirstOrDefault() ?? string.Empty;
            if (IsEditMode)
                await LoadEditableSessionsAsync();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadCreateContractsAsync()
    {
        ErrorMessage = null;
        ResetCreateState();
        try
        {
            var all = await billingContractService.ListAsync();
            Contracts = all.Where(c => c.IsActive).ToList();
            if (Contracts.Count == 1)
            {
                SelectedContractId = Contracts[0].Id;
                await LoadCreateSessionsAsync();
            }
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Contracts = [];
        }
    }

    private async Task LoadCreateSessionsAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedContractId))
            return;
        ErrorMessage = null;
        IsLoadingSessions = true;
        SelectableSessions = null;
        SelectedSessionIds.Clear();
        try
        {
            var sessions = await invoiceService.GetSelectableSessionsAsync(SelectedContractId);
            SelectableSessions = sessions.ToList();
            AutoSelectRecentSessions();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            SelectableSessions = [];
        }
        finally
        {
            IsLoadingSessions = false;
        }
    }

    private async Task LoadEditableSessionsAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || SelectedInvoice is null)
            return;
        if (!Lifecycle.IsDraft(SelectedInvoice.Status))
        {
            ErrorMessage = "Only draft invoices can be edited.";
            navigationManager.NavigateTo(Metadata.Invoice.Detail(Id));
            return;
        }

        ErrorMessage = null;
        IsLoadingSessions = true;
        try
        {
            var sessions = await invoiceService.GetEditableSessionsAsync(Id);
            SelectableSessions = sessions.ToList();
            SelectedSessionIds = SelectedInvoice.BilledSessionIds.ToHashSet();
            DueDate = SelectedInvoice.DueDate.ToLocalTime().Date;
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingSessions = false;
        }
    }

    private static Color GetStatusColor(string status)
    {
        if (!Lifecycle.TryNormalize(status, out var normalized))
            return Color.Default;
        return normalized switch
        {
            Status.Draft => Color.Default,
            Status.Sent => Color.Info,
            Status.Paid => Color.Success,
            Status.Overdue => Color.Error,
            _ => Color.Default
        };
    }

    private static bool IsDraft(string status)
    {
        return Lifecycle.IsDraft(status);
    }

    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedContractId) || SelectedSessionIds.Count == 0 || DueDate is null) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Generating invoice...", Severity.Info);
            var dueDateUtc = DateTime.SpecifyKind(DueDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            var request = new CreateRequest(SelectedContractId, SelectedSessionIds.ToList(), dueDateUtc);
            var invoice = await invoiceService.GenerateAsync(request);
            snackbar.Add("Invoice generated.", Severity.Success);
            navigationManager.NavigateTo(Metadata.Invoice.Detail(invoice.Id));
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || SelectedSessionIds.Count == 0 || DueDate is null) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Saving invoice...", Severity.Info);
            var dueDateUtc = DateTime.SpecifyKind(DueDate.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            var request = new EditRequest(SelectedSessionIds.ToList(), dueDateUtc);
            var invoice = await invoiceService.EditAsync(Id, request);
            snackbar.Add("Invoice updated.", Severity.Success);
            navigationManager.NavigateTo(Metadata.Invoice.Detail(invoice.Id));
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task UpdateStatusAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(NewStatus) || SelectedInvoice is null)
            return;
        IsBusy = true;
        try
        {
            snackbar.Add($"Updating invoice status to {NewStatus}...", Severity.Info);
            SelectedInvoice = await invoiceService.UpdateStatusAsync(Id, new UpdateRequest(NewStatus));
            AllowedTransitions = Lifecycle.AllowedTransitionsFrom(SelectedInvoice.Status);
            NewStatus = AllowedTransitions.FirstOrDefault() ?? string.Empty;
            snackbar.Add($"Invoice status updated to {SelectedInvoice.Status}.", Severity.Success);
            await LoadListAsync();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DownloadPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || SelectedInvoice is null)
            return;
        IsDownloading = true;
        IsBusy = true;
        try
        {
            snackbar.Add("Downloading invoice PDF...", Severity.Info);
            var bytes = await invoiceService.DownloadPdfAsync(Id);
            var base64 = Convert.ToBase64String(bytes);
            await jsRuntime.InvokeVoidAsync("downloadFile", $"{SelectedInvoice.InvoiceNumber}.pdf", "application/pdf",
                base64);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsDownloading = false;
            IsBusy = false;
        }
    }

    private async Task PreviewPdfAsync()
    {
        if (string.IsNullOrWhiteSpace(Id))
            return;
        IsPreviewing = true;
        IsBusy = true;
        try
        {
            snackbar.Add("Preparing invoice preview...", Severity.Info);
            var bytes = await invoiceService.DownloadPdfAsync(Id);
            var base64 = Convert.ToBase64String(bytes);
            var opened = await jsRuntime.InvokeAsync<bool>("openPdfInNewTab", base64);
            if (!opened)
                snackbar.Add("Preview blocked by the browser. Please allow pop-ups for this site.", Severity.Warning);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsPreviewing = false;
            IsBusy = false;
        }
    }

    private async Task SendInvoiceAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || SelectedInvoice is null)
            return;
        var confirmed = await jsRuntime.InvokeAsync<bool>("confirm",
            $"Send invoice {SelectedInvoice.InvoiceNumber} to {SelectedInvoice.ClientSnapshot.Email}?");
        if (!confirmed) return;
        IsSending = true;
        IsBusy = true;
        try
        {
            snackbar.Add("Sending invoice...", Severity.Info);
            SelectedInvoice = await invoiceService.SendAsync(Id);
            AllowedTransitions = Lifecycle.AllowedTransitionsFrom(SelectedInvoice.Status);
            NewStatus = AllowedTransitions.FirstOrDefault() ?? string.Empty;
            snackbar.Add("Invoice sent.", Severity.Success);
            await LoadListAsync();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsSending = false;
            IsBusy = false;
        }
    }

    private async Task DeleteFromListAsync(Summary invoice)
    {
        var confirmed = await jsRuntime.InvokeAsync<bool>("confirm",
            $"Delete invoice {invoice.InvoiceNumber}? This cannot be undone and its number will be reused.");
        if (!confirmed) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Deleting invoice...", Severity.Info);
            await invoiceService.DeleteAsync(invoice.Id);
            await LoadListAsync();
            snackbar.Add("Invoice deleted.", Severity.Success);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteCurrentAsync()
    {
        if (string.IsNullOrWhiteSpace(Id) || SelectedInvoice is null)
            return;
        var confirmed = await jsRuntime.InvokeAsync<bool>("confirm",
            $"Delete invoice {SelectedInvoice.InvoiceNumber}? This cannot be undone and its number will be reused.");
        if (!confirmed) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Deleting invoice...", Severity.Info);
            await invoiceService.DeleteAsync(Id);
            snackbar.Add("Invoice deleted.", Severity.Success);
            navigationManager.NavigateTo(Metadata.Invoice.Route);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OpenCreate()
    {
        navigationManager.NavigateTo($"{Metadata.Invoice.Route}?new=true");
    }

    private void CloseCreate()
    {
        navigationManager.NavigateTo(Metadata.Invoice.Route);
    }

    private void ToggleSession(string sessionId, bool selected)
    {
        if (selected)
            SelectedSessionIds.Add(sessionId);
        else
            SelectedSessionIds.Remove(sessionId);
    }

    private void SelectAll()
    {
        if (SelectableSessions is null) return;
        SelectedSessionIds = SelectableSessions.Select(s => s.SessionId).ToHashSet();
    }

    private void ClearSelection()
    {
        SelectedSessionIds.Clear();
    }

    private void AutoSelectRecentSessions()
    {
        SelectedSessionIds.Clear();
        if (SelectableSessions is null || SelectableSessions.Count == 0) return;
        for (var i = 0; i < SelectableSessions.Count; i++)
        {
            SelectedSessionIds.Add(SelectableSessions[i].SessionId);
            if (i == SelectableSessions.Count - 1) break;
            var gap = SelectableSessions[i + 1].StartTime - SelectableSessions[i].EndTime;
            if (gap > AutoSelectGapThreshold) break;
        }
    }

    private static string FormatHours(int minutes)
    {
        var hours = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours:F2}h";
    }

    private static string FormatDuration(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private static DateTime CalculateDefaultDueDate(DateTime currentDate)
    {
        var daysUntilNextMonday = ((int)DayOfWeek.Monday - (int)currentDate.DayOfWeek + 7) % 7;
        if (daysUntilNextMonday == 0)
            daysUntilNextMonday = 7;
        return currentDate.Date.AddDays(daysUntilNextMonday + 7);
    }

    private void ResetCreateState()
    {
        Contracts = null;
        SelectableSessions = null;
        SelectedSessionIds.Clear();
        SelectedContractId = string.Empty;
        DueDate = CalculateDefaultDueDate(DateTime.Today);
    }

    private void ClearEditState()
    {
        SelectableSessions = null;
        SelectedSessionIds.Clear();
        DueDate = SelectedInvoice?.DueDate.ToLocalTime().Date;
    }
}