using MudBlazor;

namespace IscrizioneManager.Web.Layout
{
  public static class AppTheme
  {
    public static readonly MudTheme Theme = new()
    {
      PaletteLight = new PaletteLight
      {
        Primary = "#1F4E8C",
        Secondary = "#E8A317",
        Tertiary = "#2E8B57",
        AppbarBackground = "#1F4E8C",
        Background = "#F5F7FB",
        DrawerBackground = "#FFFFFF",
        DrawerText = "rgba(0,0,0,0.75)",
        Success = "#2E7D32",
      },
      PaletteDark = new PaletteDark
      {
        Primary = "#7FA7E0",
        Secondary = "#F2B84B",
        Tertiary = "#5CC08A",
        AppbarBackground = "#16243A",
        Background = "#10161F",
        Surface = "#18212D",
        DrawerBackground = "#141C27",
      },
      LayoutProperties = new LayoutProperties
      {
        DefaultBorderRadius = "10px",
      },
    };
  }
}
