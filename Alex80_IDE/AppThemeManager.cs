using System;
using Avalonia;
using Avalonia.Styling;

namespace Alex80_IDE;

/// <summary>
/// Applica la variante di tema scelta nelle preferenze. I colori vivono in
/// Styles/Tokens.axaml (ThemeDictionaries Light/Dark): qui si commuta solo la variante,
/// i controlli seguono automaticamente tramite DynamicResource.
/// </summary>
public static class AppThemeManager
{
    public static event Action<AppTheme>? ThemeChanged;

    private static readonly UserSettings Settings = UserSettings.Load();

    public static AppTheme CurrentTheme { get; private set; } = ParseTheme(Settings.Theme);

    /// <summary>Applica il tema salvato all'avvio dell'applicazione.</summary>
    public static void Initialize(Application application)
    {
        application.RequestedThemeVariant = ToVariant(CurrentTheme);
    }

    public static void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        Settings.Theme = theme.ToString();
        Settings.Save();

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = ToVariant(theme);
        }

        ThemeChanged?.Invoke(theme);
    }

    private static ThemeVariant ToVariant(AppTheme theme) =>
        theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

    private static AppTheme ParseTheme(string? theme) =>
        Enum.TryParse<AppTheme>(theme, true, out var parsed) ? parsed : AppTheme.Dark;
}
