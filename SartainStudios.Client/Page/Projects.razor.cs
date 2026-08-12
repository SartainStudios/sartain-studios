using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
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
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private static readonly string[] Statuses = ["Active", "Archived"];
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

    protected override async Task OnInitializedAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var role = authState.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        CanManage = role is "Owner" or "Admin";
        _lastId = Id;
        IsEditMode = EditRequested && CanManage;
        if (!string.IsNullOrEmpty(Id))
            await LoadProjectAsync();
        else
            await LoadProjectsAsync();
        if (CanManage && (IsEditMode || CreateRequested))
            await EnsureClientsLoadedAsync();
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

        var prevEditMode = IsEditMode;
        IsEditMode = EditRequested && CanManage;
        if (prevEditMode && !IsEditMode && SelectedProject is not null)
            PopulateEditFields(SelectedProject);
        if (Id != _lastId)
        {
            _lastId = Id;
            ErrorMessage = null;
            if (!string.IsNullOrEmpty(Id))
            {
                SelectedProject = null;
                await LoadProjectAsync();
            }
            else
            {
                SelectedProject = null;
                await LoadProjectsAsync();
            }
        }

        if (CanManage && IsEditMode)
            await EnsureClientsLoadedAsync();
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
        }
    }

    private async Task EnsureClientsLoadedAsync()
    {
        if (Clients.Count > 0)
            return;
        try
        {
            Clients = (await clientService.ListAsync()).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            Clients = [];
        }
    }

    private void PopulateEditFields(ProjectSummary project)
    {
        ClientId = project.ClientId;
        Name = project.Name;
        Description = project.Description;
        Status = project.Status;
    }

    private async Task OpenCreateAsync()
    {
        await EnsureClientsLoadedAsync();
        ShowCreate = true;
    }

    private void OpenCreate()
    {
        _ = OpenCreateAsync();
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