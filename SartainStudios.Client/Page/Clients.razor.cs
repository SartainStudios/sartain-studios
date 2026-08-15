using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service.Caching;
using SartainStudios.Schema;
using SartainStudios.Schema.Client;
using CreateInput = SartainStudios.Client.Component.ClientCreateDialog.CreateInput;
using ClientService = SartainStudios.Client.Service.Client;
using InvoiceService = SartainStudios.Client.Service.Invoice;

namespace SartainStudios.Client.Page;

public sealed partial class Clients(
    ClientService clientService,
    InvoiceService invoiceService,
    DataCache cache,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar) : IDisposable
{
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
    private List<Summary>? ClientSummaries { get; set; }
    private Summary? SelectedClient { get; set; }
    private List<SartainStudios.Schema.Invoice.Summary> Invoices { get; set; } = [];
    private bool IsLoading { get; set; }
    private bool IsLoadingInvoices { get; set; }
    private string CompanyName { get; set; } = string.Empty;
    private string ContactPerson { get; set; } = string.Empty;
    private Address Address { get; set; } = new();
    private string Email { get; set; } = string.Empty;
    private string PhoneNumber { get; set; } = string.Empty;
    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsEditMode { get; set; }
    private bool ShowCreate { get; set; }
    private bool CanManage { get; set; }
    private bool IsDetailView => !string.IsNullOrEmpty(Id);
    private decimal TotalBilled => Invoices.Sum(i => i.TotalAmount);

    public void Dispose()
    {
        cache.Changed -= OnCacheChanged;
    }

    protected override async Task OnInitializedAsync()
    {
        _lastId = Id;
        cache.Changed += OnCacheChanged;

        var dataTask = string.IsNullOrEmpty(Id) ? LoadClientsAsync() : LoadClientAsync();

        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var role = authState.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        CanManage = role is "Owner" or "Admin";
        IsEditMode = EditRequested && CanManage;

        await dataTask;
    }

    private void OnCacheChanged(string key)
    {
        var isRelevant = string.IsNullOrEmpty(Id)
            ? key == CacheKeys.ClientList
            : key == CacheKeys.Client(Id);
        if (!isRelevant) return;
        _ = InvokeAsync(ApplyBackgroundRefreshAsync);
    }

    private async Task ApplyBackgroundRefreshAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(Id))
            {
                ClientSummaries = (await clientService.ListAsync()).ToList();
            }
            else
            {
                SelectedClient = await clientService.GetAsync(Id);
                if (!IsEditMode) PopulateEditFields(SelectedClient);
            }

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
            OpenCreate();
        }
        else if (!CreateRequested)
        {
            _createRequestHandled = false;
        }

        var prevEditMode = IsEditMode;
        IsEditMode = EditRequested && CanManage;
        if (prevEditMode && !IsEditMode && SelectedClient is not null)
            PopulateEditFields(SelectedClient);
        if (Id != _lastId)
        {
            _lastId = Id;
            ErrorMessage = null;
            if (!string.IsNullOrEmpty(Id))
            {
                SelectedClient = null;
                Invoices = [];
                await LoadClientAsync();
            }
            else
            {
                SelectedClient = null;
                Invoices = [];
                await LoadClientsAsync();
            }
        }
    }

    private async Task LoadClientsAsync()
    {
        ErrorMessage = null;
        try
        {
            ClientSummaries = (await clientService.ListAsync()).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            ClientSummaries = [];
        }
    }

    private async Task LoadClientAsync()
    {
        IsLoading = true;
        IsLoadingInvoices = true;
        ErrorMessage = null;

        var id = Id!;

        var clientTask = clientService.GetAsync(id);
        var invoicesTask = invoiceService.ListAsync(id);

        await Task.WhenAll(ApplyClientAsync(clientTask), ApplyInvoicesAsync(invoicesTask));
    }

    private async Task ApplyClientAsync(Task<Summary> clientTask)
    {
        try
        {
            SelectedClient = await clientTask;
            PopulateEditFields(SelectedClient);
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

    private async Task ApplyInvoicesAsync(Task<IReadOnlyList<SartainStudios.Schema.Invoice.Summary>> invoicesTask)
    {
        try
        {
            Invoices = (await invoicesTask).ToList();
        }
        catch (Exception ex)
        {
            Invoices = [];
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingInvoices = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void PopulateEditFields(Summary client)
    {
        CompanyName = client.CompanyName;
        ContactPerson = client.ContactPerson;
        Address = client.Address;
        Email = client.Email;
        PhoneNumber = client.PhoneNumber;
    }

    private void OpenCreate()
    {
        ShowCreate = true;
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
            snackbar.Add("Creating client...", Severity.Info);
            await clientService.CreateAsync(new CreateRequest(
                input.CompanyName,
                input.ContactPerson,
                input.Address,
                input.Email,
                input.PhoneNumber));
            ShowCreate = false;
            snackbar.Add("Client created.", Severity.Success);
            await LoadClientsAsync();
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
            await clientService.UpdateAsync(Id!,
                new UpdateRequest(CompanyName, ContactPerson, Address, Email, PhoneNumber));
            snackbar.Add("Client updated.", Severity.Success);
            SelectedClient = await clientService.GetAsync(Id!);
            PopulateEditFields(SelectedClient);
            navigationManager.NavigateTo(Metadata.Client.Detail(Id!));
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

    private async Task DeleteAsync(Summary client)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Deleting client...", Severity.Info);
            await clientService.DeleteAsync(client.Id);
            snackbar.Add("Client deleted.", Severity.Success);
            await LoadClientsAsync();
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

    private static Color GetStatusColor(string status)
    {
        return status switch
        {
            "Draft" => Color.Default,
            "Sent" => Color.Info,
            "Paid" => Color.Success,
            "Overdue" => Color.Error,
            _ => Color.Default
        };
    }
}