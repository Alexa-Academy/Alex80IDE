using System;
using Avalonia;
using Avalonia.Styling;

namespace Alex80_IDE;

/// <summary>
/// Applica la variante di tema scelta nelle preferenze. I colori vivono in
/// Styles/Tokens.axaml (ThemeDictionaries Light/Dark): qui si commuta solo la variante,
/// i controlli seguono automaticamente tramite DynamicResource.
/// <para>
/// <see cref="AppTheme.System"/> non viene risolto qui: diventa
/// <see cref="ThemeVariant.Default"/>, che Avalonia traduce nell'aspetto della
/// piattaforma e tiene aggiornato da solo quando quello cambia. Leggere una volta
/// l'impostazione del sistema e fissarla avrebbe voluto dire restare indietro al
/// primo passaggio da chiaro a scuro.
/// </para>
/// </summary>
public static class AppThemeManager
{
    public static event Action<AppTheme>? ThemeChanged;

    public static AppTheme CurrentTheme { get; private set; } = ParseTheme(UserSettings.Load().Theme);

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
        var settings = UserSettings.Load();
        settings.Theme = theme.ToString();
        settings.Save();

        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = ToVariant(theme);
        }

        ThemeChanged?.Invoke(theme);
    }

    private static ThemeVariant ToVariant(AppTheme theme) => theme switch
    {
        AppTheme.Light => ThemeVariant.Light,
        AppTheme.System => ThemeVariant.Default,
        _ => ThemeVariant.Dark
    };

    private static AppTheme ParseTheme(string? theme) =>
        Enum.TryParse<AppTheme>(theme, true, out var parsed) ? parsed : AppTheme.Dark;
}
