using Microsoft.AspNetCore.Components;
using MudBlazor;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Billing;
using SartainStudios.Schema.WorkSession;
using PageMetadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Component;

public sealed partial class SessionTimer(
    WorkSession workSession,
    ISnackbar snackbar) : IDisposable
{
    [Parameter] public IReadOnlyList<Summary> Contracts { get; set; } = [];
    [Parameter] public OnboardingStatusResult? Onboarding { get; set; }

    [Parameter] public Task<State>? InitialStateTask { get; set; }

    [Parameter] public EventCallback OnChanged { get; set; }
    [Parameter] public EventCallback<TimeSpan> OnElapsedChanged { get; set; }
    private State? State { get; set; }
    private Session? CurrentSession => State?.CurrentSession;

    private IReadOnlyList<Summary> AvailableContracts =>
        Contracts.Where(contract => contract.IsActive).ToList();

    private bool NeedsClient => Onboarding is not null && !Onboarding.HasClient;
    private bool NeedsProject => Onboarding is not null && Onboarding.HasClient && !Onboarding.HasProject;

    private string EmptyStateMessage =>
        NeedsClient
            ? "Add a client before starting a timer."
            : NeedsProject
                ? "Add a project before starting a timer."
                : "Create an active billing contract before starting a timer.";

    private string EmptyStateRoute =>
        NeedsClient
            ? PageMetadata.Client.Route
            : NeedsProject
                ? PageMetadata.Project.Route
                : PageMetadata.BillingContract.Route;

    private string EmptyStateButtonText =>
        NeedsClient
            ? "Add a Client"
            : NeedsProject
                ? "Add a Project"
                : "Add a Billing Contract";

    private bool IsRunning => State?.HasRunningSession == true && CurrentSession is not null;
    private bool IsBusy { get; set; }
    private bool IsLoading { get; set; } = true;
    private string? SelectedContractId { get; set; }
    private string? ErrorMessage { get; set; }
    private TimeSpan CurrentElapsed { get; set; }

    private double MinuteProgressPercent =>
        CurrentElapsed <= TimeSpan.Zero ? 0 : CurrentElapsed.TotalSeconds % 60 / 60 * 100;

    private int SecondsToNextMinute =>
        60 - (int)(Math.Max(0, CurrentElapsed.TotalSeconds) % 60);

    private TimeSpan ServerOffset { get; set; }
    private CancellationTokenSource? ClockCancellation { get; set; }
    private bool InitialStateConsumed { get; set; }

    public void Dispose()
    {
        ClockCancellation?.Cancel();
        ClockCancellation?.Dispose();
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        if (IsRunning)
            StartClock();
    }

    protected override void OnParametersSet()
    {
        if (IsRunning && CurrentSession is not null)
        {
            SelectedContractId = CurrentSession.ContractId;
            return;
        }

        if (!string.IsNullOrWhiteSpace(SelectedContractId) &&
            AvailableContracts.Any(contract => contract.Id == SelectedContractId)) return;
        SelectedContractId = AvailableContracts.FirstOrDefault()?.Id;
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            State = await GetStateAsync();
            ServerOffset = State.ServerTime - DateTime.UtcNow;
            if (State.HasRunningSession && State.CurrentSession is not null)
            {
                SelectedContractId = State.CurrentSession.ContractId;
                CurrentElapsed = GetCurrentUtc() - State.CurrentSession.StartTime;
            }
            else
            {
                CurrentElapsed = TimeSpan.Zero;
                SelectedContractId ??= AvailableContracts.FirstOrDefault()?.Id;
            }

            await OnElapsedChanged.InvokeAsync(CurrentElapsed);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            State = null;
            CurrentElapsed = TimeSpan.Zero;
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private Task<State> GetStateAsync()
    {
        if (InitialStateConsumed || InitialStateTask is null)
            return workSession.GetCurrentAsync();
        InitialStateConsumed = true;
        return InitialStateTask;
    }

    private void StartClock()
    {
        ClockCancellation?.Cancel();
        ClockCancellation?.Dispose();
        ClockCancellation = new CancellationTokenSource();
        _ = RunClockAsync(ClockCancellation.Token);
    }

    private void StopClock()
    {
        ClockCancellation?.Cancel();
        ClockCancellation?.Dispose();
        ClockCancellation = null;
    }

    private async Task RunClockAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                if (CurrentSession is null) continue;
                CurrentElapsed = GetCurrentUtc() - CurrentSession.StartTime;
                await OnElapsedChanged.InvokeAsync(CurrentElapsed);
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
        if (string.IsNullOrWhiteSpace(SelectedContractId)) return;
        await ExecuteAsync(async () =>
        {
            var state = await workSession.StartAsync(SelectedContractId);
            ApplyState(state);
            StartClock();
            snackbar.Add("Timer started.", Severity.Success);
            await OnChanged.InvokeAsync();
        });
    }

    private async Task StopAsync()
    {
        await ExecuteAsync(async () =>
        {
            var state = await workSession.StopAsync();
            StopClock();
            ApplyState(state);
            snackbar.Add("Timer stopped.", Severity.Success);
            await OnChanged.InvokeAsync();
        });
    }

    private async Task DiscardAsync()
    {
        if (CurrentSession is null) return;
        await ExecuteAsync(async () =>
        {
            await workSession.DiscardAsync(CurrentSession.SessionId);
            StopClock();
            await LoadAsync();
            snackbar.Add("Timer discarded.", Severity.Success);
            await OnChanged.InvokeAsync();
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

    private void ApplyState(State state)
    {
        State = state;
        ServerOffset = state.ServerTime - DateTime.UtcNow;
        if (state.HasRunningSession && state.CurrentSession is not null)
        {
            CurrentElapsed = GetCurrentUtc() - state.CurrentSession.StartTime;
            SelectedContractId = state.CurrentSession.ContractId;
        }
        else
        {
            CurrentElapsed = TimeSpan.Zero;
            SelectedContractId ??= AvailableContracts.FirstOrDefault()?.Id;
        }
    }

    private DateTime GetCurrentUtc()
    {
        return DateTime.UtcNow + ServerOffset;
    }

    private static string DisplayContract(Summary contract)
    {
        var targetHours = Math.Round(contract.ExpectedMinutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{contract.ProjectName} · {contract.ServiceProvided} ({targetHours:F2}h target)";
    }

    private static string FormatDuration(TimeSpan elapsed)
    {
        var totalMinutes = Math.Max(0, (int)Math.Floor(elapsed.TotalMinutes));
        return $"{totalMinutes / 60:00}:{totalMinutes % 60:00}";
    }
}