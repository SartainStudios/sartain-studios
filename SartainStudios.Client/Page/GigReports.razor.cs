using MudBlazor;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Page;

public sealed partial class GigReports(GigReport gigReport, ISnackbar snackbar)
{
    private const int DailyDaysShown = 14;

    private Report? Report { get; set; }
    private int Weeks { get; set; } = 12;
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;

    private IEnumerable<WeekSummary> WeeksNewestFirst => Report?.Weeks.Reverse() ?? [];

    private IReadOnlyList<DailyTotal> RecentDays =>
        Report?.Daily.TakeLast(DailyDaysShown).ToList() ?? [];

    private string[] DailyLabels => RecentDays.Select(day => day.Date.ToString("MMM d")).ToArray();

    private List<ChartSeries<double>> DailySeries =>
    [
        new() { Name = "Earnings", Data = RecentDays.Select(day => (double)day.Gross).ToArray() },
        new() { Name = "Profit", Data = RecentDays.Select(day => (double)day.Profit).ToArray() }
    ];

    private string[] WeeklyLabels => Report?.Weeks.Select(week => week.WeekStart.ToString("MMM d")).ToArray() ?? [];

    private List<ChartSeries<double>> WeeklySeries =>
    [
        new() { Name = "Earnings", Data = Report?.Weeks.Select(week => (double)week.Gross).ToArray() ?? [] },
        new() { Name = "Profit", Data = Report?.Weeks.Select(week => (double)week.Profit).ToArray() ?? [] }
    ];

    private List<ChartSeries<double>> HourlyRateSeries =>
    [
        new()
        {
            Name = "Hourly rate",
            Data = Report?.Weeks.Select(week => (double)(week.GrossHourlyRate ?? 0m)).ToArray() ?? []
        }
    ];

    private string[] WeekdayLabels =>
        Report?.WeekdayAverages.Select(day => day.Day.ToString()[..3]).ToArray() ?? [];

    private List<ChartSeries<double>> WeekdaySeries =>
    [
        new()
        {
            Name = "Average earnings",
            Data = Report?.WeekdayAverages.Select(day => (double)day.AverageGross).ToArray() ?? []
        },
        new()
        {
            Name = "Hourly rate",
            Data = Report?.WeekdayAverages.Select(day => (double)(day.GrossHourlyRate ?? 0m)).ToArray() ?? []
        }
    ];

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task OnWeeksChangedAsync(int weeks)
    {
        Weeks = weeks;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            Report = await gigReport.GetAsync(Weeks);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}