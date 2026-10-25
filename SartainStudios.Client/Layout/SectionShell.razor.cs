using Microsoft.AspNetCore.Components;

namespace SartainStudios.Client.Layout;

public sealed partial class SectionShell
{
    private bool _drawerOpen = true;

    [Parameter] public string HomeIcon { get; set; } = string.Empty;
    [Parameter] public string HomeRoute { get; set; } = string.Empty;
    [Parameter] public RenderFragment? NavLinks { get; set; }
    [Parameter] public RenderFragment? AccountMenuItems { get; set; }
    [Parameter] public bool ShowBuildInfo { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }
}