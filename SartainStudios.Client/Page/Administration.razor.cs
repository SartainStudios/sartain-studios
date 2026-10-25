using Microsoft.AspNetCore.Components;
using UserAdministrationService = SartainStudios.Client.Service.UserAdministration;

namespace SartainStudios.Client.Page;

public sealed partial class Administration(
    UserAdministrationService userAdministration,
    NavigationManager navigationManager)
{
    private static readonly AdministrationItem[] Items =
    [
        new(
            Metadata.Administration.Users.Name,
            "View, update, and delete users of the application",
            Metadata.Administration.Users.Icon,
            Metadata.Administration.UsersRoute),
        new(
            Metadata.SystemHealth.Info.Name,
            "Check the status of the application's services",
            Metadata.SystemHealth.Info.Icon,
            Metadata.SystemHealth.Route)
    ];

    private bool IsAuthorized { get; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            IsAuthorized = await userAdministration.HasAccessAsync();
        }
        catch (Exception)
        {
            IsAuthorized = false;
        }

        if (!IsAuthorized)
            navigationManager.NavigateTo(Metadata.NotFound.Route, false, true);
    }

    private sealed record AdministrationItem(string Title, string Description, string Icon, string Route);
}