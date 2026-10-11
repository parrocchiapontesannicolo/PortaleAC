using MudBlazor;

namespace IscrizioneManager.Web.Layout
{
  /// <summary>
  /// Stile del sito della parrocchia (tema WordPress "Hemingway Rewritten"):
  /// barra antracite, titoli Raleway, testo Lato, accento verde acqua #1ABC9C.
  /// In chiaro il verde è più scuro (#117A65) perché #1ABC9C su bianco non è leggibile.
  /// </summary>
  public static class AppTheme
  {
    private static readonly string[] Lato = { "Lato", "Helvetica Neue", "Arial", "sans-serif" };
    private static readonly string[] Raleway = { "Raleway", "Helvetica Neue", "Arial", "sans-serif" };

    public static readonly MudTheme Theme = new()
    {
      PaletteLight = new PaletteLight
      {
        Primary = "#117A65",
        PrimaryContrastText = "#FFFFFF",
        Secondary = "#A85F00",
        SecondaryContrastText = "#FFFFFF",
        Tertiary = "#4A5DAF",
        Info = "#1F6FB2",
        Success = "#2E7D32",
        AppbarBackground = "#1D1D1D",
        AppbarText = "#FFFFFF",
        Background = "#F4F4F2",
        Surface = "#FFFFFF",
        DrawerBackground = "#FFFFFF",
        DrawerText = "#444444",
        DrawerIcon = "#6E6E6E",
        TextPrimary = "#333333",
        TextSecondary = "#6E6E6E",
        ActionDefault = "#6E6E6E",
        LinesDefault = "#E0E0E0",
        TableLines = "#E8E8E8",
      },
      PaletteDark = new PaletteDark
      {
        Primary = "#1ABC9C",
        PrimaryContrastText = "#06241D",
        Secondary = "#F5B041",
        SecondaryContrastText = "#2A1B00",
        Tertiary = "#8EA2E8",
        Info = "#64B5F6",
        Success = "#66BB6A",
        AppbarBackground = "#141414",
        AppbarText = "#FFFFFF",
        Background = "#121212",
        Surface = "#1E1E1E",
        DrawerBackground = "#181818",
        DrawerText = "rgba(255,255,255,0.80)",
        DrawerIcon = "rgba(255,255,255,0.60)",
        TextPrimary = "rgba(255,255,255,0.87)",
        TextSecondary = "rgba(255,255,255,0.60)",
        LinesDefault = "rgba(255,255,255,0.12)",
        TableLines = "rgba(255,255,255,0.10)",
      },
      Typography = new Typography
      {
        Default = new DefaultTypography { FontFamily = Lato },
        H1 = new H1Typography { FontFamily = Raleway },
        H2 = new H2Typography { FontFamily = Raleway },
        H3 = new H3Typography { FontFamily = Raleway },
        H4 = new H4Typography { FontFamily = Raleway },
        H5 = new H5Typography { FontFamily = Raleway, FontWeight = "700" },
        H6 = new H6Typography { FontFamily = Raleway, FontWeight = "700" },
        Button = new ButtonTypography { FontFamily = Lato, FontWeight = "700", LetterSpacing = ".06em" },
        Overline = new OverlineTypography { FontFamily = Raleway, FontWeight = "700", LetterSpacing = ".16em" },
      },
      LayoutProperties = new LayoutProperties
      {
        DefaultBorderRadius = "6px",
      },
    };
  }
}
