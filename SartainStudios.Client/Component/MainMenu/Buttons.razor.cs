using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using SartainStudios.Client.Service;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Component.MainMenu;

public partial class Buttons : ComponentBase
{
    private const string ActionIcon = Icons.Material.Filled.ArrowForward;

    private static readonly MenuItem AdministrationItem = new(
        "Administration",
        "Manage users and monitor system health",
        Metadata.Administration.Index.Icon,
        [
            "View, update, and delete users",
            "Check system health"
        ],
        Metadata.Administration.IndexRoute,
        "Open administration");

    private static readonly MenuItem[] Items =
    [
        new(
            "Request Software",
            "We make your app/website",
            Icons.Material.Filled.Rocket,
            [
                "Website",
                "App",
                "API"
            ],
            Metadata.Tool.RequestSoftwareRoute,
            "Start your request"),
        new(
            "Invoicing App",
            "Invoice your clients for hours worked",
            Metadata.Invoicing.SectionIcon,
            [
                "Manage your clients",
                "Start/Stop button for tracking time",
                "Send invoices to tracked hours"
            ],
            Metadata.Invoicing.DashboardRoute,
            "Launch Invoicing App"),
        new(
            "Gig Earnings",
            "Calculate your earnings from each gig",
            Metadata.GigEarnings.SectionIcon,
            [
                "Start/Stop button for each session",
                "Log earnings from every gig app you use",
                "Track expenses and mileage"
            ],
            Metadata.GigEarnings.CurrentSessionRoute,
            "Launch Gig Earnings")
    ];

    private bool IsAdministrator { get; set; }

    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;

    [Inject] private UserAdministration UserAdministration { get; set; } = null!;

    private IEnumerable<MenuItem> VisibleItems => IsAdministrator ? [.. Items, AdministrationItem] : Items;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (state.User.Identity?.IsAuthenticated != true)
                return;
            IsAdministrator = await UserAdministration.HasAccessAsync();
        }
        catch (Exception)
        {
            IsAdministrator = false;
        }
    }

    private sealed record MenuItem(
        string Title,
        string Tagline,
        string Icon,
        string[] Highlights,
        string Route,
        string ActionText);
}