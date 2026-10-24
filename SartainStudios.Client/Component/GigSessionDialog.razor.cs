using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Component;

public enum GigSessionDialogMode
{
    Stop,
    Edit,
    Create
}

public sealed partial class GigSessionDialog
{
    private static readonly DialogOptions DialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true,
        CloseButton = true,
        BackdropClick = false
    };

    private static readonly (string Value, string Label)[] VehicleOptions =
    [
        (nameof(VehicleType.Car), "Car"),
        (nameof(VehicleType.Motorcycle), "Motorcycle"),
        (nameof(VehicleType.Bicycle), "Bicycle"),
        (nameof(VehicleType.EBike), "E-bike"),
        (nameof(VehicleType.Other), "Other")
    ];

    private bool _wasVisible;

    [Inject] private IJSRuntime JsRuntime { get; set; } = null!;
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public GigSessionDialogMode Mode { get; set; } = GigSessionDialogMode.Edit;
    [Parameter] public GigSessionDto? Session { get; set; }
    [Parameter] public IReadOnlyList<Source> Sources { get; set; } = [];
    [Parameter] public IReadOnlyList<Category> Categories { get; set; } = [];
    [Parameter] public bool MileageDeductionEnabled { get; set; }
    [Parameter] public bool IsBusy { get; set; }
    [Parameter] public EventCallback<GigSessionInput> OnSave { get; set; }

    private string? ErrorMessage { get; set; }
    private DateTime? StartDate { get; set; }
    private TimeSpan? StartTime { get; set; }
    private DateTime? EndDate { get; set; }
    private TimeSpan? EndTime { get; set; }
    private decimal? StartOdometer { get; set; }
    private decimal? EndOdometer { get; set; }
    private string Vehicle { get; set; } = nameof(VehicleType.Car);
    private string Notes { get; set; } = string.Empty;
    private List<EarningRow> Earnings { get; set; } = [];
    private List<ExpenseRow> Expenses { get; set; } = [];

    private string Title => Mode switch
    {
        GigSessionDialogMode.Stop => "End session",
        GigSessionDialogMode.Create => "Add session",
        _ => "Edit session"
    };

    private string SaveText => Mode == GigSessionDialogMode.Stop ? "Stop" : "Save";

    private string? VehicleHelperText =>
        MileageDeductionEnabled
            ? VehicleTypes.IsDeductible(Vehicle)
                ? "Counts toward the IRS mileage deduction."
                : "Not a motor vehicle, so these miles are not deductible."
            : null;

    protected override async Task OnParametersSetAsync()
    {
        var opening = Visible && !_wasVisible;
        _wasVisible = Visible;
        if (!opening)
            return;
        Populate();
        if (Mode == GigSessionDialogMode.Stop)
            Vehicle = await VehicleMemory.LoadAsync(JsRuntime) ?? Vehicle;
    }

    private async Task OnVehicleChangedAsync(string? selected)
    {
        Vehicle = string.IsNullOrWhiteSpace(selected) ? nameof(VehicleType.Car) : selected;
        if (Mode == GigSessionDialogMode.Stop)
            await VehicleMemory.SaveAsync(JsRuntime, Vehicle);
    }

    private void Populate()
    {
        ErrorMessage = null;
        var session = Mode == GigSessionDialogMode.Create ? null : Session;
        if (session is null)
        {
            var now = DateTime.Now;
            StartDate = now.Date;
            StartTime = new TimeSpan(Math.Max(0, now.Hour - 1), now.Minute, 0);
            EndDate = now.Date;
            EndTime = new TimeSpan(now.Hour, now.Minute, 0);
            StartOdometer = null;
            EndOdometer = null;
            Vehicle = nameof(VehicleType.Car);
            Notes = string.Empty;
            Earnings = [];
            Expenses = [];
            return;
        }

        var start = session.StartTime.ToLocalTime();
        StartDate = start.Date;
        StartTime = start.TimeOfDay;
        if (session.EndTime is { } end)
        {
            var localEnd = end.ToLocalTime();
            EndDate = localEnd.Date;
            EndTime = localEnd.TimeOfDay;
        }
        else
        {
            EndDate = null;
            EndTime = null;
        }

        StartOdometer = session.StartOdometer;
        EndOdometer = session.EndOdometer;
        Vehicle = string.IsNullOrWhiteSpace(session.VehicleType)
            ? nameof(VehicleType.Car)
            : session.VehicleType;
        Notes = session.Notes;
        Earnings = session.Earnings
            .Select(entry => new EarningRow { SourceId = entry.SourceId, Amount = entry.Amount, Note = entry.Note })
            .ToList();
        Expenses = session.Expenses
            .Select(entry => new ExpenseRow { CategoryId = entry.CategoryId, Amount = entry.Amount, Note = entry.Note })
            .ToList();
    }

    private IEnumerable<Source> SourceChoices(string? current)
    {
        return Sources.Where(source => !source.IsHidden || source.Id == current);
    }

    private IEnumerable<Category> CategoryChoices(string? current)
    {
        return Categories.Where(category => !category.IsHidden || category.Id == current);
    }

    private void AddEarning()
    {
        Earnings.Add(new EarningRow { SourceId = SourceChoices(null).FirstOrDefault()?.Id });
    }

    private void AddExpense()
    {
        Expenses.Add(new ExpenseRow { CategoryId = CategoryChoices(null).FirstOrDefault()?.Id });
    }

    private Task OnVisibleChangedAsync(bool visible)
    {
        return VisibleChanged.InvokeAsync(visible);
    }

    private Task CloseAsync()
    {
        return VisibleChanged.InvokeAsync(false);
    }

    private async Task SaveAsync()
    {
        ErrorMessage = null;
        var input = Build();
        if (input is null || !OnSave.HasDelegate)
            return;
        await OnSave.InvokeAsync(input);
    }

    private GigSessionInput? Build()
    {
        DateTime startUtc = default;
        DateTime? endUtc = null;
        if (StartDate is null || StartTime is null)
        {
            ErrorMessage = "Start date and time are required.";
            return null;
        }

        startUtc = DateTime.SpecifyKind(StartDate.Value.Date + StartTime.Value, DateTimeKind.Local)
            .ToUniversalTime();
        if (Mode == GigSessionDialogMode.Stop)
        {
            if (Session is { } original &&
                original.StartTime.ToLocalTime() is var originalLocal &&
                originalLocal.Date + new TimeSpan(originalLocal.Hour, originalLocal.Minute, 0) ==
                StartDate.Value.Date + StartTime.Value)
                startUtc = DateTime.SpecifyKind(original.StartTime, DateTimeKind.Utc);
            if (startUtc > DateTime.UtcNow.AddMinutes(1))
            {
                ErrorMessage = "Start time cannot be in the future.";
                return null;
            }
        }
        else
        {
            var needsEnd = Mode == GigSessionDialogMode.Create || Session is { IsOpen: false };
            if (needsEnd || EndDate is not null || EndTime is not null)
            {
                if (EndDate is null || EndTime is null)
                {
                    ErrorMessage = "End date and time are required.";
                    return null;
                }

                endUtc = DateTime.SpecifyKind(EndDate.Value.Date + EndTime.Value, DateTimeKind.Local)
                    .ToUniversalTime();
                if (endUtc < startUtc)
                {
                    ErrorMessage = "End time cannot be earlier than the start time.";
                    return null;
                }
            }
        }

        if (StartOdometer is not null && EndOdometer is not null && EndOdometer < StartOdometer)
        {
            ErrorMessage = "End odometer cannot be below the start odometer.";
            return null;
        }

        var earnings = new List<EarningEntry>();
        foreach (var row in Earnings.Where(row => row.Amount is not null || row.SourceId is not null))
        {
            if (string.IsNullOrWhiteSpace(row.SourceId))
            {
                ErrorMessage = "Choose a source for each earning, or remove the row.";
                return null;
            }

            earnings.Add(new EarningEntry(row.SourceId, row.Amount ?? 0m, row.Note));
        }

        var expenses = new List<ExpenseEntry>();
        foreach (var row in Expenses.Where(row => row.Amount is not null || row.CategoryId is not null))
        {
            if (string.IsNullOrWhiteSpace(row.CategoryId))
            {
                ErrorMessage = "Choose a category for each expense, or remove the row.";
                return null;
            }

            expenses.Add(new ExpenseEntry(row.CategoryId, row.Amount ?? 0m, row.Note));
        }

        if (earnings.Any(entry => entry.Amount < 0) || expenses.Any(entry => entry.Amount < 0))
        {
            ErrorMessage = "Amounts cannot be negative.";
            return null;
        }

        return new GigSessionInput(
            startUtc,
            endUtc,
            StartOdometer,
            EndOdometer,
            Vehicle,
            Notes,
            earnings,
            expenses);
    }

    public sealed record GigSessionInput(
        DateTime StartTimeUtc,
        DateTime? EndTimeUtc,
        decimal? StartOdometer,
        decimal? EndOdometer,
        string VehicleType,
        string Notes,
        IReadOnlyList<EarningEntry> Earnings,
        IReadOnlyList<ExpenseEntry> Expenses);

    private sealed class EarningRow
    {
        public string? SourceId { get; set; }
        public decimal? Amount { get; set; }
        public string? Note { get; set; }
    }

    private sealed class ExpenseRow
    {
        public string? CategoryId { get; set; }
        public decimal? Amount { get; set; }
        public string? Note { get; set; }
    }
}