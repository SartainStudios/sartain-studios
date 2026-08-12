using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using MudBlazor;

namespace SartainStudios.Client;

public partial class App(NavigationManager navigationManager, IJSRuntime jsRuntime) : ComponentBase, IDisposable
{
    private bool _isDarkMode;
    private MudThemeProvider _mudThemeProvider = null!;

    public static MudTheme Theme => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#594AE2",
            PrimaryDarken = "#4335C9",
            PrimaryLighten = "#776BEB",
            Secondary = "#00C853",
            SecondaryDarken = "#00A243",
            SecondaryLighten = "#33D375",
            Tertiary = "#FF9800",
            Info = "#2196F3",
            Success = "#4CAF50",
            Warning = "#FFC107",
            Error = "#F44336",
            Dark = "#27272F",
            Background = "#F8F9FA",
            BackgroundGray = "#F0F2F5",
            Surface = "#FFFFFF",
            AppbarBackground = "#FFFFFF",
            AppbarText = "#27272F",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#424242",
            DrawerIcon = "#616161",
            TextPrimary = "#27272F",
            TextSecondary = "#6E6E78",
            TextDisabled = "#A0A0AB",
            ActionDefault = "#6E6E78",
            ActionDisabled = "#A0A0AB",
            ActionDisabledBackground = "#E0E0E6",
            LinesDefault = "#E0E0E6",
            LinesInputs = "#C8C8D0",
            TableLines = "#E0E0E6",
            TableStriped = "#F8F9FA",
            TableHover = "#F0F2F5",
            Divider = "#E0E0E6",
            DividerLight = "#F0F2F5"
        },
        PaletteDark = new PaletteDark
        {
            Primary = "#776BEB",
            PrimaryDarken = "#594AE2",
            PrimaryLighten = "#958BEF",
            Secondary = "#33D375",
            Tertiary = "#FFB74D",
            Info = "#64B5F6",
            Success = "#81C784",
            Warning = "#FFD54F",
            Error = "#E57373",
            Dark = "#0D0E12",
            Background = "#121318",
            BackgroundGray = "#1A1B23",
            Surface = "#1E1F29",
            AppbarBackground = "#1E1F29",
            AppbarText = "#E2E2E6",
            DrawerBackground = "#1E1F29",
            DrawerText = "#E2E2E6",
            DrawerIcon = "#A0A0AB",
            TextPrimary = "#E2E2E6",
            TextSecondary = "#A0A0AB",
            TextDisabled = "#6E6E78",
            ActionDefault = "#A0A0AB",
            ActionDisabled = "#42424F",
            ActionDisabledBackground = "#272733",
            LinesDefault = "#272733",
            LinesInputs = "#3F3F4E",
            TableLines = "#272733",
            TableStriped = "#1A1B23",
            TableHover = "#272733",
            Divider = "#272733",
            DividerLight = "#1A1B23"
        }
    };

    public void Dispose()
    {
        navigationManager.LocationChanged -= OnLocationChanged;
    }

    protected override void OnInitialized()
    {
        navigationManager.LocationChanged += OnLocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await TrackPageView(navigationManager.Uri);
            _isDarkMode = await _mudThemeProvider.GetSystemDarkModeAsync();
            await _mudThemeProvider.WatchSystemDarkModeAsync(OnSystemPreferenceChanged);
            StateHasChanged();
        }
    }

    private Task OnSystemPreferenceChanged(bool isDarkMode)
    {
        _isDarkMode = isDarkMode;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async void OnLocationChanged(object? sender, LocationChangedEventArgs args)
    {
        await TrackPageView(args.Location);
    }

    private async Task TrackPageView(string location)
    {
        var path = navigationManager.ToBaseRelativePath(location);
        await jsRuntime.InvokeVoidAsync("trackPageView", "/" + path);
    }
}