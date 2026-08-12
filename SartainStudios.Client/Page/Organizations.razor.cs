using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Schema;
using SartainStudios.Schema.Membership;
using SartainStudios.Schema.Organization;
using MembershipService = SartainStudios.Client.Service.Membership;
using OrganizationService = SartainStudios.Client.Service.Organization;
using UpdateRequest = SartainStudios.Schema.Organization.UpdateRequest;

namespace SartainStudios.Client.Page;

public sealed partial class Organizations(
    OrganizationService service,
    MembershipService membershipService,
    AuthenticationStateProvider authenticationStateProvider,
    NavigationManager navigationManager,
    ISnackbar snackbar)
{
    private const string OwnerRole = "Owner";
    private const string AdminRole = "Admin";
    private const string MemberRole = "Member";
    private const string StatusActive = "Active";
    private const string StatusInvited = "Invited";
    private const string StatusSuspended = "Suspended";
    private bool _createRequestHandled;

    [Parameter]
    [SupplyParameterFromQuery(Name = "tab")]
    public string? Tab { get; set; }

    [Parameter]
    [SupplyParameterFromQuery(Name = "new")]
    public bool CreateRequested { get; set; }

    private MudForm EditForm { get; set; } = null!;
    private List<OrganizationSummary>? OrganizationSummaries { get; set; }
    private OrganizationSummary? ActiveOrganization { get; set; }
    private List<Summary>? Rows { get; set; }
    private bool HasOrganizations => OrganizationSummaries is { Count: > 0 };
    private bool CanSwitchOrganizations => SelectableOrganizations.Count > 1;

    private List<OrganizationSummary> SelectableOrganizations =>
        OrganizationSummaries?
            .Where(o => string.Equals(o.MembershipStatus, StatusActive, StringComparison.OrdinalIgnoreCase))
            .ToList() ?? [];

    private string? SelectedOrganizationId { get; set; }

    private int ActiveTabIndex { get; set; }

    private bool ShowCreate { get; set; }

    private string Name { get; set; } = string.Empty;
    private string Email { get; set; } = string.Empty;
    private Address Address { get; set; } = new();
    private string PhoneNumber { get; set; } = string.Empty;

    private string CurrentRole { get; set; } = string.Empty;
    private string CurrentUserId { get; set; } = string.Empty;
    private string InviteEmail { get; set; } = string.Empty;
    private string InviteRole { get; set; } = MemberRole;

    private string? ErrorMessage { get; set; }
    private bool IsBusy { get; set; }
    private bool IsLoading { get; set; } = true;

    private bool CanEdit => string.Equals(ActiveOrganization?.Role, OwnerRole, StringComparison.OrdinalIgnoreCase);
    private bool IsOwner => string.Equals(CurrentRole, OwnerRole, StringComparison.OrdinalIgnoreCase);
    private bool IsAdmin => string.Equals(CurrentRole, AdminRole, StringComparison.OrdinalIgnoreCase);
    private bool CanInvite => IsOwner || IsAdmin;

    private IEnumerable<string> AvailableInviteRoles =>
        IsOwner ? Enum.GetNames<RoleType>() : new[] { AdminRole, MemberRole };

    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
    }

    protected override void OnParametersSet()
    {
        ActiveTabIndex = string.Equals(Tab, "members", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        if (CreateRequested && !_createRequestHandled)
        {
            _createRequestHandled = true;
            OpenCreate();
        }
    }

    private async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            CurrentRole = state.User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            CurrentUserId = state.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? string.Empty;
            var organizationId = state.User.FindFirst("organizationId")?.Value;

            var list = await service.ListMineAsync();
            OrganizationSummaries = list.ToList();

            if (string.IsNullOrWhiteSpace(organizationId))
            {
                ActiveOrganization = null;
                SelectedOrganizationId = null;
                Rows = null;
                Name = string.Empty;
                Email = string.Empty;
                Address = new Address();
                PhoneNumber = string.Empty;
                return;
            }

            ActiveOrganization = await service.GetAsync(organizationId);
            SelectedOrganizationId = ActiveOrganization.Id;
            Name = ActiveOrganization.Name;
            Email = ActiveOrganization.Email;
            Address = ActiveOrganization.Address ?? new Address();
            PhoneNumber = ActiveOrganization.PhoneNumber ?? string.Empty;

            var memberships = await membershipService.ListAsync();
            Rows = memberships.ToList();
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

    private void OpenCreate()
    {
        ShowCreate = true;
    }

    private async Task CreateAsync(CreateRequest request)
    {
        IsBusy = true;
        try
        {
            snackbar.Add("Creating organization...", Severity.Info);
            var created = await service.CreateAsync(request);
            ShowCreate = false;
            snackbar.Add($"Created \"{created.Name}\".", Severity.Success);
            navigationManager.NavigateTo(Metadata.Organization.Route, true);
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
        if (ActiveOrganization is null) return;

        await EditForm.ValidateAsync();
        if (!EditForm.IsValid) return;

        IsBusy = true;
        try
        {
            snackbar.Add("Saving...", Severity.Info);
            ActiveOrganization = await service.UpdateAsync(ActiveOrganization.Id, new UpdateRequest(
                Name,
                Address.HasValue ? Address : null,
                string.IsNullOrWhiteSpace(Email) ? null : Email,
                string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber));
            Name = ActiveOrganization.Name;
            Email = ActiveOrganization.Email;
            Address = ActiveOrganization.Address ?? new Address();
            PhoneNumber = ActiveOrganization.PhoneNumber ?? string.Empty;
            snackbar.Add("Organization updated.", Severity.Success);
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

    private async Task SwitchOrganizationAsync(string organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId)) return;

        IsBusy = true;
        try
        {
            snackbar.Add("Switching organizations...", Severity.Info);
            await service.SwitchAsync(organizationId);
            navigationManager.NavigateTo(Metadata.Organization.Route, true);
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

    private async Task OnOrganizationSelectedAsync(string? organizationId)
    {
        if (string.IsNullOrWhiteSpace(organizationId)
            || string.Equals(organizationId, ActiveOrganization?.Id, StringComparison.Ordinal))
            return;

        SelectedOrganizationId = organizationId;
        await SwitchOrganizationAsync(organizationId);
    }

    private async Task InviteAsync()
    {
        IsBusy = true;
        try
        {
            snackbar.Add($"Sending invitation to {InviteEmail}...", Severity.Info);
            await membershipService.InviteAsync(new InviteRequest(InviteEmail, InviteRole));
            snackbar.Add($"Invitation sent to {InviteEmail}.", Severity.Success);
            InviteEmail = string.Empty;
            InviteRole = MemberRole;
            await LoadAsync();
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

    private async Task UpdateRoleAsync(Summary membership, string newRole)
    {
        if (string.Equals(membership.Role, newRole, StringComparison.OrdinalIgnoreCase))
            return;

        IsBusy = true;
        try
        {
            snackbar.Add($"Updating role for {membership.Email} to {newRole}...", Severity.Info);
            var updated = await membershipService.UpdateRoleAsync(membership.Id,
                new SartainStudios.Schema.Membership.UpdateRequest(newRole));
            var index = Rows?.FindIndex(m => m.Id == updated.Id) ?? -1;
            if (index >= 0 && Rows is not null) Rows[index] = updated;
            snackbar.Add("Role updated.", Severity.Success);
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

    private async Task RemoveAsync(Summary membership)
    {
        IsBusy = true;
        try
        {
            snackbar.Add($"Removing {membership.Email}...", Severity.Info);
            await membershipService.RemoveAsync(membership.Id);
            Rows?.RemoveAll(m => m.Id == membership.Id);
            snackbar.Add($"Removed {membership.Email}.", Severity.Success);
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

    private bool CanChangeRole(Summary membership)
    {
        if (!IsOwner) return false;
        return !(membership.UserId == CurrentUserId && string.Equals(membership.Role, OwnerRole,
                                                        StringComparison.OrdinalIgnoreCase)
                                                    && (Rows?.Count(m =>
                                                        string.Equals(m.Role, OwnerRole,
                                                            StringComparison.OrdinalIgnoreCase)
                                                        && string.Equals(m.Status, StatusActive,
                                                            StringComparison.OrdinalIgnoreCase)) ?? 0) <= 1);
    }

    private bool CanRemove(Summary membership)
    {
        if (!CanInvite) return false;
        if (membership.UserId == CurrentUserId) return false;
        if (string.Equals(membership.Role, OwnerRole, StringComparison.OrdinalIgnoreCase) && !IsOwner) return false;
        return true;
    }

    private static Color StatusColor(string status)
    {
        return status switch
        {
            StatusActive => Color.Success,
            StatusInvited => Color.Warning,
            StatusSuspended => Color.Error,
            _ => Color.Default
        };
    }
}