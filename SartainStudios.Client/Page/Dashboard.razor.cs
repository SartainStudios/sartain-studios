using MudBlazor;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Service;
using SartainStudios.Schema.Billing;
using SartainStudios.Schema.WorkSession;
using BillingContractService = SartainStudios.Client.Service.BillingContract;

namespace SartainStudios.Client.Page;

public sealed partial class Dashboard(
    BillingContractService billingContractService,
    WorkSession workSessionService,
    OnboardingStatus onboardingStatusService,
    ISnackbar snackbar)
{
    private const string TutorialVideoUrl = "https://youtu.be/hswWOqx8Uu4";

    private List<Summary> ActiveContracts { get; set; } = [];
    private List<History> Sessions { get; set; } = [];
    private Dictionary<string, int> BaseProgressMinutesByContract { get; set; } = [];
    private List<ContractProgressItem> ProgressItems { get; set; } = [];
    private TimeBudget? Budget { get; set; }
    private DateTime BudgetLoadedAtUtc { get; set; } = DateTime.UtcNow;
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsLoadingContracts { get; set; } = true;
    private bool IsLoadingSessions { get; set; } = true;
    private bool IsLoadingProgress { get; set; } = true;
    private bool IsLoadingBudget { get; set; } = true;
    private bool IsLoadingOnboarding { get; set; } = true;
    private OnboardingStatusResult? Onboarding { get; set; }

    private Task<State>? InitialSessionStateTask { get; set; }

    private int? LastAppliedElapsedMinutes { get; set; }
    private bool RequiresSetup => Onboarding?.HasBillingContract == false;

    private string NextSetupRoute => Onboarding switch
    {
        { HasClient: false } => Metadata.Client.Route,
        { HasProject: false } => Metadata.Project.Route,
        _ => Metadata.BillingContract.Route
    };

    private string NextSetupButtonText => Onboarding switch
    {
        { HasClient: false } => "Add your first client",
        { HasProject: false } => "Add your first project",
        _ => "Create your billing contract"
    };

    private bool HasRunningSession => Sessions.Any(session => session.IsRunning);

    private int LiveDayWorkedMinutes
    {
        get
        {
            if (Budget is null)
                return 0;
            if (!HasRunningSession)
                return Budget.DayWorkedMinutes;
            var minutesSinceLoad = Math.Max(0, (int)(DateTime.UtcNow - BudgetLoadedAtUtc).TotalMinutes);
            var minutesLeftInDay = Math.Max(0, (int)(DateTime.Now.Date.AddDays(1) - DateTime.Now).TotalMinutes);
            return Budget.DayWorkedMinutes + Math.Min(minutesSinceLoad, minutesLeftInDay);
        }
    }

    private int LiveDayRemainingMinutes =>
        Budget is null ? 0 : Math.Max(0, Budget.DayTargetMinutes - LiveDayWorkedMinutes);

    private DateTime? EstimatedStopTime =>
        Budget is null || LiveDayRemainingMinutes <= 0
            ? null
            : DateTime.Now.AddMinutes(LiveDayRemainingMinutes);

    private string StopTimeMessage
    {
        get
        {
            if (Budget is null)
                return string.Empty;
            if (LiveDayRemainingMinutes == 0)
                return "Daily target met - you can stop now.";
            var stopTime = EstimatedStopTime!.Value.ToString("h:mm tt");
            return HasRunningSession
                ? $"Stop at {stopTime} to hit today's target."
                : $"Start now and stop at {stopTime}.";
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        IsLoadingContracts = true;
        IsLoadingSessions = true;
        IsLoadingProgress = true;
        IsLoadingBudget = true;
        IsLoadingOnboarding = true;
        ErrorMessage = null;
        LastAppliedElapsedMinutes = null;

        var (dayStart, dayEnd, weekStart, weekEnd) = GetLocalBudgetRanges();
        InitialSessionStateTask = workSessionService.GetCurrentAsync();
        ObserveFailure(InitialSessionStateTask);
        var contractsTask = billingContractService.ListAsync();
        var sessionsTask = workSessionService.ListAsync(take: 25);
        var progressTask = workSessionService.GetProgressAsync();
        var budgetTask = workSessionService.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);
        var onboardingTask = onboardingStatusService.GetAsync();

        await Task.WhenAll(
            ApplyContractsAsync(contractsTask),
            ApplySessionsAsync(sessionsTask),
            ApplyProgressAsync(progressTask),
            ApplyBudgetAsync(budgetTask),
            ApplyOnboardingAsync(onboardingTask));
    }

    private async Task RefreshSessionDataAsync()
    {
        IsLoadingSessions = true;
        IsLoadingProgress = true;
        IsLoadingBudget = true;
        ErrorMessage = null;
        LastAppliedElapsedMinutes = null;

        var (dayStart, dayEnd, weekStart, weekEnd) = GetLocalBudgetRanges();
        var sessionsTask = workSessionService.ListAsync(take: 25);
        var progressTask = workSessionService.GetProgressAsync();
        var budgetTask = workSessionService.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);

        await Task.WhenAll(
            ApplySessionsAsync(sessionsTask),
            ApplyProgressAsync(progressTask),
            ApplyBudgetAsync(budgetTask));
    }

    private async Task ApplyContractsAsync(Task<IReadOnlyList<Summary>> contractsTask)
    {
        try
        {
            var contracts = await contractsTask;
            ActiveContracts = contracts
                .Where(contract => contract.IsActive)
                .OrderBy(contract => contract.ProjectName)
                .ToList();
        }
        catch (Exception ex)
        {
            ActiveContracts = [];
            ReportError(ex);
        }
        finally
        {
            IsLoadingContracts = false;
            BuildProgressItems();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ApplySessionsAsync(Task<IReadOnlyList<History>> sessionsTask)
    {
        try
        {
            Sessions = (await sessionsTask).ToList();
        }
        catch (Exception ex)
        {
            Sessions = [];
            ReportError(ex);
        }
        finally
        {
            IsLoadingSessions = false;
            BuildProgressItems();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ApplyProgressAsync(Task<IReadOnlyList<Progress>> progressTask)
    {
        try
        {
            BaseProgressMinutesByContract = (await progressTask)
                .ToDictionary(item => item.ContractId, item => item.LoggedMinutes);
        }
        catch (Exception ex)
        {
            BaseProgressMinutesByContract = [];
            ReportError(ex);
        }
        finally
        {
            IsLoadingProgress = false;
            BuildProgressItems();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ApplyBudgetAsync(Task<TimeBudget> budgetTask)
    {
        try
        {
            Budget = await budgetTask;
            BudgetLoadedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            Budget = null;
            ReportError(ex);
        }
        finally
        {
            IsLoadingBudget = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ApplyOnboardingAsync(Task<OnboardingStatusResult> onboardingTask)
    {
        try
        {
            Onboarding = await onboardingTask;
        }
        catch (Exception ex)
        {
            Onboarding = null;
            ReportError(ex);
        }
        finally
        {
            IsLoadingOnboarding = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void ReportError(Exception exception)
    {
        if (ErrorMessage == exception.Message)
            return;
        ErrorMessage = exception.Message;
        snackbar.Add(exception.Message, Severity.Error);
    }

    private static void ObserveFailure(Task task)
    {
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private Task HandleElapsedChanged(TimeSpan elapsed)
    {
        var elapsedMinutes = Math.Max(0, (int)elapsed.TotalMinutes);

        if (elapsedMinutes == LastAppliedElapsedMinutes)
            return Task.CompletedTask;

        var index = Sessions.FindIndex(session => session.IsRunning);
        if (index < 0)
        {
            LastAppliedElapsedMinutes = null;
            return Task.CompletedTask;
        }

        LastAppliedElapsedMinutes = elapsedMinutes;
        Sessions[index] = Sessions[index] with { ElapsedMinutes = elapsedMinutes };
        BuildProgressItems();
        return InvokeAsync(StateHasChanged);
    }

    private async Task DiscardAsync(History session)
    {
        if (!session.CanDiscard)
            return;
        IsBusy = true;
        try
        {
            snackbar.Add("Discarding...", Severity.Info);
            await workSessionService.DiscardAsync(session.SessionId);
            await RefreshSessionDataAsync();
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

    private void BuildProgressItems()
    {
        var runningSession = Sessions.FirstOrDefault(session => session.IsRunning);
        var runningMinutes = runningSession?.ElapsedMinutes ?? 0;
        ProgressItems = ActiveContracts
            .Select(contract => new ContractProgressItem(
                contract,
                BaseProgressMinutesByContract.TryGetValue(contract.Id, out var loggedMinutes) ? loggedMinutes : 0,
                runningSession is not null && runningSession.ContractId == contract.Id ? runningMinutes : 0))
            .OrderByDescending(item => item.LoggedMinutes)
            .ThenBy(item => item.Contract.ProjectName)
            .ToList();
    }

    private static string FormatHours(int minutes)
    {
        var hours = Math.Round(minutes / 60m, 2, MidpointRounding.AwayFromZero);
        return $"{hours:F2}h";
    }

    private static (DateTime DayStart, DateTime DayEnd, DateTime WeekStart, DateTime WeekEnd) GetLocalBudgetRanges()
    {
        var todayLocal = DateTime.Now.Date;
        var dayStart = todayLocal.ToUniversalTime();
        var dayEnd = todayLocal.AddDays(1).ToUniversalTime();
        var daysSinceMonday = ((int)todayLocal.DayOfWeek + 6) % 7;
        var weekStartLocal = todayLocal.AddDays(-daysSinceMonday);
        var weekStart = weekStartLocal.ToUniversalTime();
        var weekEnd = weekStartLocal.AddDays(7).ToUniversalTime();
        return (dayStart, dayEnd, weekStart, weekEnd);
    }

    private static Color GetProgressColor(ContractProgressItem item)
    {
        if (item.RemainingMinutes < 0)
            return Color.Error;
        if (item.RemainingMinutes == 0)
            return Color.Success;
        return item.ProgressPercent >= 75 ? Color.Warning : Color.Primary;
    }

    private static Color GetStatusColor(ContractProgressItem item)
    {
        if (item.RemainingMinutes < 0)
            return Color.Error;
        if (item.RemainingMinutes == 0)
            return Color.Success;
        return Color.Info;
    }
}