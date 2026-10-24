using Microsoft.JSInterop;
using MudBlazor;
using SartainStudios.Client.Component;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Gig;

namespace SartainStudios.Client.Page;

public sealed partial class GigEarnings(
    GigSession gigSession,
    GigSource gigSource,
    GigReport gigReport,
    IJSRuntime jsRuntime,
    ISnackbar snackbar) : IDisposable
{
    private CancellationTokenSource? _clock;
    private TimeSpan _serverOffset;

    private GigSessionDto? CurrentSession { get; set; }
    private IReadOnlyList<Source> Sources { get; set; } = [];
    private IReadOnlyList<Category> Categories { get; set; } = [];
    private Report? Report { get; set; }
    private TimeSpan CurrentElapsed { get; set; }
    private decimal? StartOdometer { get; set; }
    private string Vehicle { get; set; } = nameof(VehicleType.Car);
    private string? ErrorMessage { get; set; }
    private bool IsLoading { get; set; } = true;
    private bool IsLoadingReport { get; set; } = true;
    private bool IsBusy { get; set; }
    private bool StopDialogVisible { get; set; }
    private bool IsRunning => CurrentSession is { IsOpen: true };

    public void Dispose()
    {
        StopClock();
    }

    protected override async Task OnInitializedAsync()
    {
        Vehicle = await VehicleMemory.LoadAsync(jsRuntime) ?? Vehicle;
        await Task.WhenAll(LoadSessionAsync(), LoadOptionsAsync(), LoadReportAsync());
    }

    private async Task OnVehicleChangedAsync(string? selected)
    {
        Vehicle = string.IsNullOrWhiteSpace(selected) ? nameof(VehicleType.Car) : selected;
        await VehicleMemory.SaveAsync(jsRuntime, Vehicle);
    }

    private async Task LoadSessionAsync()
    {
        try
        {
            ApplyState(await gigSession.GetCurrentAsync());
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadOptionsAsync()
    {
        try
        {
            var sources = gigSource.ListSourcesAsync();
            var categories = gigSource.ListCategoriesAsync();
            Sources = await sources;
            Categories = await categories;
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
    }

    private async Task LoadReportAsync()
    {
        IsLoadingReport = true;
        try
        {
            Report = await gigReport.GetAsync(2);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
        }
        finally
        {
            IsLoadingReport = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ApplyState(GigSessionState state)
    {
        CurrentSession = state.CurrentSession;
        _serverOffset = state.ServerTime - DateTime.UtcNow;
        if (IsRunning)
        {
            CurrentElapsed = GetNowUtc() - CurrentSession!.StartTime;
            StartClock();
        }
        else
        {
            CurrentElapsed = TimeSpan.Zero;
            StopClock();
        }
    }

    private DateTime GetNowUtc()
    {
        return DateTime.UtcNow + _serverOffset;
    }

    private void StartClock()
    {
        StopClock();
        _clock = new CancellationTokenSource();
        _ = RunClockAsync(_clock.Token);
    }

    private void StopClock()
    {
        _clock?.Cancel();
        _clock?.Dispose();
        _clock = null;
    }

    private async Task RunClockAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (CurrentSession is null) continue;
                CurrentElapsed = GetNowUtc() - CurrentSession.StartTime;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // Clock stopped intentionally.
        }
    }

    private async Task StartAsync()
    {
        await ExecuteAsync(async () =>
        {
            ApplyState(await gigSession.StartAsync(StartOdometer, Vehicle));
            StartOdometer = null;
            snackbar.Add("Session started.", Severity.Success);
        });
    }

    private void OpenStopDialog()
    {
        StopDialogVisible = true;
    }

    private Task StopAsync(GigSessionDialog.GigSessionInput input)
    {
        return StopWithAsync(new StopGigSessionRequest(
            null, input.EndOdometer, input.Notes, input.Earnings, input.Expenses, input.VehicleType,
            input.StartTimeUtc, input.StartOdometer));
    }

    private async Task StopWithAsync(StopGigSessionRequest request)
    {
        await ExecuteAsync(async () =>
        {
            await gigSession.StopAsync(request);
            StopDialogVisible = false;
            ApplyState(new GigSessionState(null, DateTime.UtcNow));
            snackbar.Add("Session saved.", Severity.Success);
            await LoadReportAsync();
        });
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action();
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
}