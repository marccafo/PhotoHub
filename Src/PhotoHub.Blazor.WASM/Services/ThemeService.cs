using MudBlazor;

namespace PhotoHub.Blazor.WASM.Services;

public class ThemeService
{
    public MudTheme Theme { get; } = new MudTheme()
    {
        PaletteLight = new PaletteLight()
        {
            Primary = "#4285f4",
            AppbarBackground = "#ffffff",
            AppbarText = "#202124",
            Background = "#f8f9fa",
            Surface = "#ffffff",
            DrawerBackground = "#ffffff",
            TextPrimary = "#202124",
            TextSecondary = "#5f6368",
        },
        PaletteDark = new PaletteDark()
        {
            Primary = "#4285f4",
            Surface = "#1a1a1a",
            Background = "#121212",
            BackgroundGray = "#1e1e1e",
            AppbarBackground = "#1a1a1a",
            DrawerBackground = "#1a1a1a",
            TextPrimary = "#e1e1e1",
            TextSecondary = "#aaaaaa",
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "8px",
        }
    };
}
