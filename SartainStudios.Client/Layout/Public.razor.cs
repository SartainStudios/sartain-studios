namespace SartainStudios.Client.Layout;

public partial class Public
{
    private bool? _isOnline;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var report = await HealthService.GetLivenessAsync();
            _isOnline = string.Equals(report.Status, "Healthy", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            _isOnline = false;
        }
    }
}