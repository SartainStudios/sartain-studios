using MudBlazor;
using SartainStudios.Client.Schema;
using SartainStudios.Client.Service;
using SartainStudios.Client.Service.Authentication;
using SartainStudios.Schema.Billing;
using SartainStudios.Schema.WorkSession;
using BillingContractService = SartainStudios.Client.Service.BillingContract;
using ClientService = SartainStudios.Client.Service.Client;
using OrganizationService = SartainStudios.Client.Service.Organization;
using ProjectService = SartainStudios.Client.Service.Project;

namespace SartainStudios.Client.Page;

public sealed partial class Dashboard(
    BillingContractService billingContractService,
    WorkSession workSessionService,
    ClientService clientService,
    ProjectService projectService,
    OrganizationService organizationService,
    TokenStore tokenStore,
    ISnackbar snackbar)
{
    private List<Summary> ActiveContracts { get; set; } = [];
    private List<History> Sessions { get; set; } = [];
    private Dictionary<string, int> BaseProgressMinutesByContract { get; set; } = [];
    private List<ContractProgressItem> ProgressItems { get; set; } = [];
    private TimeBudget? Budget { get; set; }
    private DateTime BudgetLoadedAtUtc { get; set; } = DateTime.UtcNow;
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsLoading { get; set; } = true;
    private OnboardingStatusResult? Onboarding { get; set; }
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
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var (dayStart, dayEnd, weekStart, weekEnd) = GetLocalBudgetRanges();
            var contractsTask = billingContractService.ListAsync();
            var sessionsTask = workSessionService.ListAsync(take: 25);
            var progressTask = workSessionService.GetProgressAsync();
            var budgetTask = workSessionService.GetTimeBudgetAsync(dayStart, dayEnd, weekStart, weekEnd);
            var onboardingTask = BuildOnboardingAsync(contractsTask, sessionsTask);
            await Task.WhenAll(contractsTask, sessionsTask, progressTask, budgetTask, onboardingTask);
            var contracts = await contractsTask;
            ActiveContracts = contracts
                .Where(contract => contract.IsActive)
                .OrderBy(contract => contract.ProjectName)
                .ToList();
            Sessions = (await sessionsTask).ToList();
            BaseProgressMinutesByContract = (await progressTask)
                .ToDictionary(item => item.ContractId, item => item.LoggedMinutes);
            BuildProgressItems();
            Budget = await budgetTask;
            BudgetLoadedAtUtc = DateTime.UtcNow;
            Onboarding = await onboardingTask;
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            ActiveContracts = [];
            Sessions = [];
            BaseProgressMinutesByContract = [];
            ProgressItems = [];
            Budget = null;
            Onboarding = null;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task<OnboardingStatusResult> BuildOnboardingAsync(
        Task<IReadOnlyList<Summary>> contractsTask,
        Task<IReadOnlyList<History>> sessionsTask)
    {
        var organizationCustomizedTask = GetOrganizationCustomizedAsync();
        var clientsTask = clientService.ListAsync();
        var projectsTask = projectService.ListAsync();
        await Task.WhenAll(organizationCustomizedTask, clientsTask, projectsTask, contractsTask, sessionsTask);
        return new OnboardingStatusResult(
            await organizationCustomizedTask,
            (await clientsTask).Count > 0,
            (await projectsTask).Count > 0,
            (await contractsTask).Count > 0,
            (await sessionsTask).Count > 0,
            false);
    }

    private async Task<bool> GetOrganizationCustomizedAsync()
    {
        var session = await tokenStore.LoadAsync();
        if (string.IsNullOrWhiteSpace(session?.OrganizationId))
            return false;
        try
        {
            var organization = await organizationService.GetAsync(session.OrganizationId);
            var hasAddress = organization.Address?.HasValue ?? false;
            var hasPhoneNumber = !string.IsNullOrWhiteSpace(organization.PhoneNumber);
            return hasAddress && hasPhoneNumber;
        }
        catch
        {
            return false;
        }
    }

    private Task HandleElapsedChanged(TimeSpan elapsed)
    {
        var index = Sessions.FindIndex(session => session.IsRunning);
        if (index < 0)
            return Task.CompletedTask;
        Sessions[index] = Sessions[index] with { ElapsedMinutes = Math.Max(0, (int)elapsed.TotalMinutes) };
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