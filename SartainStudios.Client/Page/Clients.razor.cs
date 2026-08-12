using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Schema;
using SartainStudios.Schema.Client;
using CreateInput = SartainStudios.Client.Component.ClientCreateDialog.CreateInput;
using ClientService = SartainStudios.Client.Service.Client;
using InvoiceService = SartainStudios.Client.Service.Invoice;

namespace SartainStudios.Client.Page;

public sealed partial class Clients(
    ClientService clientService,
    InvoiceService invoiceService,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar)
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

    protected override async Task OnInitializedAsync()
    {
        var authState = await authenticationStateProvider.GetAuthenticationStateAsync();
        var role = authState.User.FindFirst(ClaimTypes.Role)?.Value ?? "";
        CanManage = role is "Owner" or "Admin";
        _lastId = Id;
        IsEditMode = EditRequested && CanManage;
        if (!string.IsNullOrEmpty(Id))
            await LoadClientAsync();
        else
            await LoadClientsAsync();
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
        try
        {
            SelectedClient = await clientService.GetAsync(Id!);
            PopulateEditFields(SelectedClient);
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
            return;
        }
        finally
        {
            IsLoading = false;
        }

        try
        {
            Invoices = (await invoiceService.ListAsync(Id!)).ToList();
        }
        catch (Exception ex)
        {
            snackbar.Add(ex.Message, Severity.Error);
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingInvoices = false;
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