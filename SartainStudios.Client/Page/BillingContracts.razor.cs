using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service.Validation;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema.Billing;
using SartainStudios.Schema.Membership;
using CreateInput = SartainStudios.Client.Component.BillingContractCreateDialog.CreateInput;
using BillingContractService = SartainStudios.Client.Service.BillingContract;
using CreateRequest = SartainStudios.Schema.Billing.CreateRequest;
using ProjectService = SartainStudios.Client.Service.Project;
using ProjectSummary = SartainStudios.Schema.Project.Summary;
using Summary = SartainStudios.Schema.Billing.Summary;
using UpdateRequest = SartainStudios.Schema.Billing.UpdateRequest;

namespace SartainStudios.Client.Page;

public sealed partial class BillingContracts(
    BillingContractService billingContractService,
    ProjectService projectService,
    DataCache cache,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar) : IDisposable
{
    private const string OwnerRole = nameof(RoleType.Owner);
    private const string AdminRole = nameof(RoleType.Administrator);
    private static readonly string[] BillingCycles = Enum.GetNames<Cycle>();
    private bool _createRequestHandled;
    private string? _lastId;
    private Task? _projectsLoad;

    [Parameter]
    [SupplyParameterFromQuery(Name = "id")]
    public string? Id { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "new")]
    public bool CreateRequested { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "edit")]
    public bool EditRequested { get; set; }

    private MudForm EditForm { get; set; } = null!;
    private List<Summary>? Contracts { get; set; }
    private List<ProjectSummary> Projects { get; set; } = [];
    private Summary? SelectedContract { get; set; }
    private bool IsLoading { get; set; }
    private string ProjectId { get; set; } = string.Empty;
    private string ServiceProvided { get; set; } = string.Empty;
    private decimal HourlyRate { get; set; }
    private decimal ExpectedHours { get; set; }
    private string BillingCycle { get; set; } = string.Empty;
    private string InvoicePrefix { get; set; } = string.Empty;
    private bool IsActive { get; set; }
    private string CurrentRole { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsEditMode { get; set; }
    private bool ShowCreate { get; set; }

    private bool CanManage => string.Equals(CurrentRole, OwnerRole, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(CurrentRole, AdminRole, StringComparison.OrdinalIgnoreCase);

    private bool IsDetailView => !string.IsNullOrEmpty(Id);

    protected override async Task OnInitializedAsync()
    {
        _lastId = Id;
        cache.Changed += OnCacheChanged;

        var contractTask = string.IsNullOrEmpty(Id) ? LoadContractsAsync() : LoadContractAsync();

        var state = await authenticationStateProvider.GetAuthenticationStateAsync();
        CurrentRole = state.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        IsEditMode = EditRequested && CanManage;

        if (IsEditMode)
            _ = EnsureProjectsAsync();

        await contractTask;
    }

    public void Dispose()
    {
        cache.Changed -= OnCacheChanged;
    }

    private void OnCacheChanged(string key)
    {
        var isRelevant = key == CacheKeys.ProjectList
                         || (string.IsNullOrEmpty(Id)
                             ? key == CacheKeys.BillingContractList(null)
                             : key == CacheKeys.BillingContract(Id));
        if (!isRelevant) return;
        _ = InvokeAsync(ApplyBackgroundRefreshAsync);
    }

    private async Task ApplyBackgroundRefreshAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(Id))
            {
                Contracts = (await billingContractService.ListAsync()).ToList();
            }
            else
            {
                SelectedContract = await billingContractService.GetAsync(Id);
                if (!IsEditMode) PopulateEditFields(SelectedContract);
            }

            if (_projectsLoad is not null) Projects = (await projectService.ListAsync()).ToList();

            StateHasChanged();
        }
        catch
        {
            // The already rendered data stays on screen when a background refresh cannot be applied.
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (CreateRequested && !_createRequestHandled)
        {
            _createRequestHandled = true;
            await OpenCreateAsync();
        }
        else if (!CreateRequested)
        {
            _createRequestHandled = false;
        }

        var previousEditMode = IsEditMode;
        IsEditMode = EditRequested && CanManage;
        if (IsEditMode && !previousEditMode)
            await EnsureProjectsAsync();
        if (previousEditMode && !IsEditMode && SelectedContract is not null)
            PopulateEditFields(SelectedContract);
        if (Id == _lastId) return;
        _lastId = Id;
        ErrorMessage = null;
        SelectedContract = null;
        if (!string.IsNullOrEmpty(Id))
            await LoadContractAsync();
        else
            await LoadContractsAsync();
    }

    private Task EnsureProjectsAsync()
    {
        return _projectsLoad ??= LoadProjectsAndRenderAsync();
    }

    private async Task LoadProjectsAndRenderAsync()
    {
        await LoadProjectsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            Projects = (await projectService.ListAsync()).ToList();
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
            Projects = [];
        }
    }

    private async Task LoadContractsAsync()
    {
        ErrorMessage = null;
        try
        {
            Contracts = (await billingContractService.ListAsync()).ToList();
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
            ErrorMessage = exception.Message;
            Contracts = [];
        }
    }

    private async Task LoadContractAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedContract = await billingContractService.GetAsync(Id!);
            PopulateEditFields(SelectedContract);
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void PopulateEditFields(Summary contract)
    {
        ProjectId = contract.ProjectId;
        ServiceProvided = contract.ServiceProvided;
        HourlyRate = contract.HourlyRate;
        ExpectedHours = contract.ExpectedMinutes / 60m;
        BillingCycle = contract.BillingCycle;
        InvoicePrefix = contract.InvoicePrefix;
        IsActive = contract.IsActive;
    }

    private async Task OpenCreateAsync()
    {
        ShowCreate = true;
        await EnsureProjectsAsync();
    }

    private Task OnCreateDialogVisibleChangedAsync(bool visible)
    {
        ShowCreate = visible;
        return Task.CompletedTask;
    }

    private static int ToMinutes(decimal hours)
    {
        return (int)Math.Round(hours * 60m, MidpointRounding.AwayFromZero);
    }

    private static string? ValidateServiceProvided(string? value)
    {
        return FieldValidators.ValidateRequiredText(value, "Service provided");
    }

    private static string? ValidateHourlyRate(decimal value)
    {
        return FieldValidators.ValidatePositiveAmount(value, "Hourly rate");
    }

    private static string? ValidateExpectedHours(decimal value)
    {
        return FieldValidators.ValidatePositiveAmount(value, "Expected hours");
    }

    private async Task CreateAsync(CreateInput input)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Creating contract...", Severity.Info);
            await billingContractService.CreateAsync(new CreateRequest(
                input.ProjectId,
                input.HourlyRate,
                ToMinutes(input.ExpectedHours),
                input.BillingCycle,
                input.ServiceProvided,
                input.InvoicePrefix.Trim().ToUpperInvariant(),
                input.IsActive));
            ShowCreate = false;
            snackbar.Add("Contract created.", Severity.Success);
            await LoadContractsAsync();
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveAsync()
    {
        await EditForm.ValidateAsync();
        if (!EditForm.IsValid) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Saving...", Severity.Info);
            SelectedContract = await billingContractService.UpdateAsync(Id!, new UpdateRequest(
                ProjectId,
                HourlyRate,
                ToMinutes(ExpectedHours),
                BillingCycle,
                ServiceProvided,
                InvoicePrefix.Trim().ToUpperInvariant(),
                IsActive));
            PopulateEditFields(SelectedContract);
            snackbar.Add("Contract updated.", Severity.Success);
            navigationManager.NavigateTo(Metadata.BillingContract.Detail(Id!));
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(Summary contract)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Deleting contract...", Severity.Info);
            await billingContractService.DeleteAsync(contract.Id);
            snackbar.Add("Contract deleted.", Severity.Success);
            await LoadContractsAsync();
        }
        catch (Exception exception)
        {
            snackbar.Add(exception.Message, Severity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }
}