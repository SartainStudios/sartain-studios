using MudBlazor;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Page;

public sealed partial class GigPreferences(GigSettings gigSettings, ISnackbar snackbar)
{
    private Preferences? Current { get; set; }
    private bool Enabled { get; set; }
    private decimal? RateOverride { get; set; }
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsBusy { get; set; }

    private string RateHelperText => Current is null
        ? string.Empty
        : $"Leave empty to use the current IRS rate of {Current.DefaultRate:0.000} per mile ({Current.RateYear}). Each session uses the rate in effect on its date.";

    protected override async Task OnInitializedAsync()
    {
        try
        {
            Apply(await gigSettings.GetAsync());
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

    private async Task SaveAsync()
    {
        IsBusy = true;
        try
        {
            Apply(await gigSettings.UpdateAsync(new UpdatePreferencesRequest(Enabled, RateOverride)));
            snackbar.Add("Settings saved.", Severity.Success);
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

    private void Apply(Preferences preferences)
    {
        Current = preferences;
        Enabled = preferences.MileageDeductionEnabled;
        RateOverride = preferences.MileageRateOverride;
    }
}