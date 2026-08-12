namespace SartainStudios.Client.Layout;

public sealed partial class Main
{
    private bool _drawerOpen = true;

    private void DrawerToggle()
    {
        _drawerOpen = !_drawerOpen;
    }
}