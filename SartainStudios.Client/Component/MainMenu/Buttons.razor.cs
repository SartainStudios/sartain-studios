using Microsoft.AspNetCore.Components;
using MudBlazor;
using Metadata = SartainStudios.Client.Page.Metadata;

namespace SartainStudios.Client.Component.MainMenu;

public partial class Buttons : ComponentBase
{
    private const string ActionIcon = Icons.Material.Filled.ArrowForward;

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

    private sealed record MenuItem(
        string Title,
        string Tagline,
        string Icon,
        string[] Highlights,
        string Route,
        string ActionText);
}