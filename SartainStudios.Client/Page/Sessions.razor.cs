using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Service;
using SartainStudios.Schema.WorkSession;

namespace SartainStudios.Client.Page;

public sealed partial class Sessions(
    WorkSession workSession,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private bool _lastEditRequested;
    private string? _lastId;

    [Parameter]
    [SupplyParameterFromQuery(Name = "id")]
    public string? Id { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "edit")]
    public bool EditRequested { get; set; }

    private List<History> Items { get; set; } = [];
    private History? Session { get; set; }
    private DateTime? StartDate { get; set; }
    private TimeSpan? StartTime { get; set; }
    private DateTime? EndDate { get; set; }
    private TimeSpan? EndTime { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsSaveDisabled => IsBusy;
    private bool IsEditView => EditRequested && !string.IsNullOrWhiteSpace(Id);
    private int RunningCount => Items.Count(x => x.IsRunning);
    private int UnbilledCount => Items.Count(x => x is { IsRunning: false, InvoiceId: null, CanDiscard: true });
    private int TotalMinutes => Items.Sum(x => x.ElapsedMinutes);

    protected override async Task OnInitializedAsync()
    {
        _lastId = Id;
        _lastEditRequested = EditRequested;
        await LoadCurrentViewAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Id == _lastId && EditRequested == _lastEditRequested)
            return;
        _lastId = Id;
        _lastEditRequested = EditRequested;
        await LoadCurrentViewAsync();
    }

    private async Task RefreshAsync()
    {
        await LoadCurrentViewAsync();
    }

    private async Task LoadCurrentViewAsync()
    {
        if (IsEditView)
        {
            Items = [];
            await LoadSessionAsync();
            return;
        }

        Session = null;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Items = (await workSession.ListAsync(take: 100)).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Items = [];
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadSessionAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Session = await workSession.GetAsync(Id!);
            PopulateEditFields(Session);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Session = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task DiscardAsync(History session)
    {
        if (!session.CanDiscard)
            return;
        IsBusy = true;
        try
        {
            await workSession.DiscardAsync(session.SessionId);
            await LoadAsync();
            snackbar.Add("Session discarded.", Severity.Success);
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

    private void PopulateEditFields(History session)
    {
        var start = session.StartTime.ToLocalTime();
        StartDate = start.Date;
        StartTime = start.TimeOfDay;
        if (session.EndTime.HasValue)
        {
            var end = session.EndTime.Value.ToLocalTime();
            EndDate = end.Date;
            EndTime = end.TimeOfDay;
            return;
        }

        EndDate = null;
        EndTime = null;
    }

    private async Task SaveAsync()
    {
        if (Session is null || !Session.CanEdit)
            return;
        if (StartDate is null || StartTime is null)
        {
            snackbar.Add("Start date and time are required.", Severity.Error);
            return;
        }

        var startLocal = DateTime.SpecifyKind(StartDate.Value.Date + StartTime.Value, DateTimeKind.Local);
        DateTime? endLocal = null;
        if (EndDate is not null || EndTime is not null)
        {
            if (EndDate is null || EndTime is null)
            {
                snackbar.Add("End date and time must both be provided.", Severity.Error);
                return;
            }

            endLocal = DateTime.SpecifyKind(EndDate.Value.Date + EndTime.Value, DateTimeKind.Local);
            if (endLocal < startLocal)
            {
                snackbar.Add("End time cannot be earlier than the start time.", Severity.Error);
                return;
            }
        }

        IsBusy = true;
        try
        {
            snackbar.Add("Saving session...", Severity.Info);
            Session = await workSession.UpdateAsync(Session.SessionId,
                new UpdateRequest(startLocal.ToUniversalTime(), endLocal?.ToUniversalTime()));
            snackbar.Add("Session updated.", Severity.Success);
            navigationManager.NavigateTo(Metadata.Invoicing.SessionsRoute);
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

    private static string FormatHours(int minutes)
    {
        var hours = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours:F2}h";
    }

    private static string GetStatusLabel(History session)
    {
        if (session.IsRunning)
            return "Running";
        if (session.InvoiceId is not null)
            return session.CanEdit ? "Draft" : "Billed";
        return session.CanDiscard ? "Unbilled" : "Recorded";
    }

    private static Color GetStatusColor(History session)
    {
        if (session.IsRunning)
            return Color.Success;
        if (session.InvoiceId is not null)
            return Color.Info;
        return session.CanDiscard ? Color.Warning : Color.Default;
    }
}