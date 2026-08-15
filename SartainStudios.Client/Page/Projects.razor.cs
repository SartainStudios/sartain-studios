using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service.Caching;
using CreateInput = SartainStudios.Client.Component.ProjectCreateDialog.CreateInput;
using CreateRequest = SartainStudios.Schema.Project.CreateRequest;
using ProjectSummary = SartainStudios.Schema.Project.Summary;
using UpdateRequest = SartainStudios.Schema.Project.UpdateRequest;
using ClientService = SartainStudios.Client.Service.Client;
using ClientSummary = SartainStudios.Schema.Client.Summary;
using ProjectService = SartainStudios.Client.Service.Project;

namespace SartainStudios.Client.Page;

public sealed partial class Projects(
    ProjectService projectService,
    ClientService clientService,
    DataCache cache,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar) : IDisposable
{
    private static readonly string[] Statuses = ["Active", "Archived"];
    private Task? _clientsLoad;
    private bool _createRequestHandled;
    private string? _lastId;

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
    private List<ProjectSummary>? ProjectSummaries { get; set; }
    private ProjectSummary? SelectedProject { get; set; }
    private List<ClientSummary> Clients { get; set; } = [];
    private bool IsLoading { get; set; }
    private string ClientId { get; set; } = string.Empty;
    private string Name { get; set; } = string.Empty;
    private string Description { get; set; } = string.Empty;
    private string Status { get; set; } = "Active";
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsEditMode { get; set; }
    private bool ShowCreate { get; set; }
    private bool CanManage { get; set; }
    private bool IsDetailView => !string.IsNullOrEmpty(Id);

    public void Dispose()
    {
        cache.Changed -= OnCacheChanged;
    }

    protected override Task OnInitializedAsync()
    {
        _lastId = Id;
        cache.Changed += OnCacheChanged;

        var projectTask = string.IsNullOrEmpty(Id) ? LoadProjectsAsync() : LoadProjectAsync();
        var clientsTask = EditRequested || CreateRequested ? EnsureClientsLoadedAsync() : Task.CompletedTask;
        var authTask = LoadAuthorizationAsync();

        return Task.WhenAll(projectTask, clientsTask, authTask);
    }

    private void OnCacheChanged(string key)
    {
        var isRelevant = key == CacheKeys.ClientList
                         || (string.IsNullOrEmpty(Id) ? key == CacheKeys.ProjectList : key == CacheKeys.Project(Id));
        if (!isRelevant) return;
        _ = InvokeAsync(ApplyBackgroundRefreshAsync);
    }

    private async Task ApplyBackgroundRefreshAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(Id))
            {
                ProjectSummaries = (await projectService.ListAsync()).ToList();
            }
            else
            {
                SelectedProject = await projectService.GetAsync(Id);
                if (!IsEditMode) PopulateEditFields(SelectedProject);
            }

            if (_clientsLoad is not null) Clients = (await clientService.ListAsync()).ToList();

            StateHasChanged();
        }
        catch
        {
            // The already rendered data stays on screen when a background refresh cannot be applied.
        }
    }

    protected override Task OnParametersSetAsync()
    {
        var prevEditMode = IsEditMode;
        IsEditMode = EditRequested && CanManage;
        if (prevEditMode && !IsEditMode && SelectedProject is not null)
            PopulateEditFields(SelectedProject);

        var projectTask = Task.CompletedTask;
        if (Id != _lastId)
        {
            _lastId = Id;
            ErrorMessage = null;
            SelectedProject = null;
            projectTask = string.IsNullOrEmpty(Id) ? LoadProjectsAsync() : LoadProjectAsync();
        }

        var clientsTask = Task.CompletedTask;
        if (CreateRequested && !_createRequestHandled)
        {
            _createRequestHandled = true;
            ShowCreate = true;
            clientsTask = EnsureClientsLoadedAsync();
        }
        else if (!CreateRequested)
        {
            _createRequestHandled = false;
        }

        if (CanManage && IsEditMode && ReferenceEquals(clientsTask, Task.CompletedTask))
            clientsTask = EnsureClientsLoadedAsync();

        return Task.WhenAll(projectTask, clientsTask);
    }

    private async Task LoadAuthorizationAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var role = authState.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        CanManage = role is "Owner" or "Admin";
        IsEditMode = EditRequested && CanManage;
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadProjectsAsync()
    {
        ErrorMessage = null;
        try
        {
            ProjectSummaries = (await projectService.ListAsync()).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            ProjectSummaries = [];
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task LoadProjectAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            SelectedProject = await projectService.GetAsync(Id!);
            PopulateEditFields(SelectedProject);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private Task EnsureClientsLoadedAsync()
    {
        return _clientsLoad ??= LoadClientsAsync();
    }

    private async Task LoadClientsAsync()
    {
        try
        {
            Clients = (await clientService.ListAsync()).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Clients = [];
            _clientsLoad = null;
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private void PopulateEditFields(ProjectSummary project)
    {
        ClientId = project.ClientId;
        Name = project.Name;
        Description = project.Description;
        Status = project.Status;
    }

    private void OpenCreate()
    {
        ShowCreate = true;
        _ = EnsureClientsLoadedAsync();
    }

    private Task OnCreateDialogVisibleChangedAsync(bool visible)
    {
        ShowCreate = visible;
        return Task.CompletedTask;
    }

    private async Task CreateAsync(CreateInput input)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Creating project...", Severity.Info);
            await projectService.CreateAsync(new CreateRequest(
                input.ClientId,
                input.Name,
                input.Description,
                input.Status));
            ShowCreate = false;
            snackbar.Add("Project created.", Severity.Success);
            await LoadProjectsAsync();
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

    private async Task SaveAsync()
    {
        await EditForm.ValidateAsync();
        if (!EditForm.IsValid) return;
        IsBusy = true;
        try
        {
            snackbar.Add("Saving...", Severity.Info);
            await projectService.UpdateAsync(Id!, new UpdateRequest(ClientId, Name, Description, Status));
            snackbar.Add("Project updated.", Severity.Success);
            SelectedProject = await projectService.GetAsync(Id!);
            PopulateEditFields(SelectedProject);
            navigationManager.NavigateTo(Metadata.Project.Detail(Id!));
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

    private async Task DeleteAsync(ProjectSummary project)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Deleting project...", Severity.Info);
            await projectService.DeleteAsync(project.Id);
            snackbar.Add("Project deleted.", Severity.Success);
            await LoadProjectsAsync();
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