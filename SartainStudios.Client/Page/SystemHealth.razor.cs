using MudBlazor;
using SartainStudios.Schema.Health;
using HealthService = SartainStudios.Client.Service.Health;

namespace SartainStudios.Client.Page;

public sealed partial class SystemHealth(HealthService healthService, ISnackbar snackbar)
{
    private HealthReportResponse? Report { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsRefreshing { get; set; }
    private string? ErrorMessage { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            Report = await healthService.GetAsync();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            Report = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try
        {
            await LoadAsync();
            if (Report is null)
                return;
            var isHealthy = string.Equals(Report.Status, "Healthy", StringComparison.Ordinal);
            snackbar.Add(
                isHealthy ? "All systems healthy." : $"System status: {Report.Status}.",
                isHealthy ? Severity.Success : Severity.Warning);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private static Color GetStatusColor(string status)
    {
        return status switch
        {
            "Healthy" => Color.Success,
            "Degraded" => Color.Warning,
            _ => Color.Error
        };
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return $"{duration.TotalMilliseconds:F0} ms";
    }
}