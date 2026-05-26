using System;
using Avalonia.Controls;

namespace Alex80_IDE;

public partial class EditorWindow : Window
{
    public EditorWindow()
    {
        InitializeComponent();
        AppThemeManager.ApplyTo(this);
        AppThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void OnThemeChanged(AppTheme theme)
    {
        AppThemeManager.ApplyTo(this);
    }

    protected override void OnClosed(EventArgs e)
    {
        AppThemeManager.ThemeChanged -= OnThemeChanged;
        base.OnClosed(e);
    }
}
