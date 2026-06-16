using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Alex80_IDE;

public static class AppThemeManager
{
    public static event Action<AppTheme>? ThemeChanged;

    private static readonly UserSettings Settings = UserSettings.Load();

    public static AppTheme CurrentTheme { get; private set; } = ParseTheme(Settings.Theme);

    public static void SetTheme(AppTheme theme)
    {
        if (CurrentTheme == theme)
        {
            return;
        }

        CurrentTheme = theme;
        Settings.Theme = theme.ToString();
        Settings.Save();
        ApplyApplicationTheme(theme);
        ThemeChanged?.Invoke(theme);
    }

    public static void ApplyTo(Window window)
    {
        ApplyApplicationTheme(CurrentTheme);
        window.RequestedThemeVariant = CurrentTheme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;

        if (CurrentTheme == AppTheme.Light)
        {
            SetBrush(window, "AppBackgroundBrush", "#EEF2F7");
            SetBrush(window, "SurfaceBrush", "#FFFFFF");
            SetBrush(window, "SurfaceRaisedBrush", "#F3F6FA");
            SetBrush(window, "SurfaceInputBrush", "#FFFFFF");
            SetBrush(window, "BorderSoftBrush", "#C9D2DF");
            SetBrush(window, "TextPrimaryBrush", "#18202B");
            SetBrush(window, "TextSecondaryBrush", "#5E6978");
            SetBrush(window, "AccentBrush", "#087D6C");
            SetBrush(window, "AccentStrongBrush", "#0D9488");
            SetBrush(window, "DangerBrush", "#C73535");
            SetBrush(window, "WarningBrush", "#9A6400");
            SetBrush(window, "DangerSurfaceBrush", "#FCE8E8");
            SetBrush(window, "DangerTextBrush", "#8B1E1E");
            SetBrush(window, "BadgeBackgroundBrush", "#DDF6F1");
            SetBrush(window, "EditorSelectionBrush", "#6638BDF8");
            SetBrush(window, "EditorSearchResultsBrush", "#66F59E0B");
        }
        else
        {
            SetBrush(window, "AppBackgroundBrush", "#111318");
            SetBrush(window, "SurfaceBrush", "#181B22");
            SetBrush(window, "SurfaceRaisedBrush", "#20242D");
            SetBrush(window, "SurfaceInputBrush", "#14171D");
            SetBrush(window, "BorderSoftBrush", "#313744");
            SetBrush(window, "TextPrimaryBrush", "#F0F4FA");
            SetBrush(window, "TextSecondaryBrush", "#98A2B3");
            SetBrush(window, "AccentBrush", "#4CC9A7");
            SetBrush(window, "AccentStrongBrush", "#2FAE8E");
            SetBrush(window, "DangerBrush", "#E66767");
            SetBrush(window, "WarningBrush", "#EAB85F");
            SetBrush(window, "DangerSurfaceBrush", "#332024");
            SetBrush(window, "DangerTextBrush", "#FFDCDC");
            SetBrush(window, "BadgeBackgroundBrush", "#1C3A35");
            SetBrush(window, "EditorSelectionBrush", "#805FB3F3");
            SetBrush(window, "EditorSearchResultsBrush", "#66F2C94C");
        }
    }

    private static void ApplyApplicationTheme(AppTheme theme)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = theme == AppTheme.Light ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    private static void SetBrush(Window window, string key, string color)
    {
        if (window.Resources.TryGetResource(key, window.ActualThemeVariant, out var resource) &&
            resource is SolidColorBrush brush)
        {
            brush.Color = Color.Parse(color);
            return;
        }

        window.Resources[key] = new SolidColorBrush(Color.Parse(color));
    }

    private static AppTheme ParseTheme(string? theme)
    {
        return Enum.TryParse<AppTheme>(theme, true, out var parsed) ? parsed : AppTheme.Dark;
    }
}
