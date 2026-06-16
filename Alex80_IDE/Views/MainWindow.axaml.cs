using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Alex80_IDE;

public partial class MainWindow : Window
{
    private EditorWindow? _editorWindow;
    private NativeMenuItem? _darkThemeMenuItem;
    private NativeMenuItem? _lightThemeMenuItem;
    private double _normalWindowWidth;
    private double _normalWindowHeight;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowSize();
        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        ConfigureSystemMenu();
        AppThemeManager.ApplyTo(this);
        UpdateThemeMenuChecks(AppThemeManager.CurrentTheme);
        AppThemeManager.ThemeChanged += OnThemeChanged;
    }

    private void RestoreWindowSize()
    {
        var settings = UserSettings.Load();

        if (settings.MainWindowWidth is >= 1160 and <= 10000)
        {
            Width = settings.MainWindowWidth.Value;
        }

        if (settings.MainWindowHeight is >= 780 and <= 10000)
        {
            Height = settings.MainWindowHeight.Value;
        }

        _normalWindowWidth = Width;
        _normalWindowHeight = Height;

        if (settings.MainWindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _normalWindowWidth = e.NewSize.Width;
        _normalWindowHeight = e.NewSize.Height;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var settings = UserSettings.Load();
        settings.MainWindowWidth = _normalWindowWidth;
        settings.MainWindowHeight = _normalWindowHeight;
        settings.MainWindowMaximized = WindowState == WindowState.Maximized;
        settings.Save();
    }

    private void OpenEditorWindow(object? sender, RoutedEventArgs e)
    {
        if (_editorWindow is not null)
        {
            _editorWindow.Activate();
            return;
        }

        _editorWindow = new EditorWindow
        {
            DataContext = DataContext
        };
        AppThemeManager.ApplyTo(_editorWindow);
        _editorWindow.Closed += (_, _) => _editorWindow = null;
        _editorWindow.Show(this);
    }

    private void OnThemeChanged(AppTheme theme)
    {
        AppThemeManager.ApplyTo(this);
        UpdateThemeMenuChecks(theme);
    }

    private void ConfigureSystemMenu()
    {
        _darkThemeMenuItem = new NativeMenuItem("Scuro")
        {
            ToggleType = NativeMenuItemToggleType.Radio
        };
        _darkThemeMenuItem.Click += (_, _) => AppThemeManager.SetTheme(AppTheme.Dark);

        _lightThemeMenuItem = new NativeMenuItem("Chiaro")
        {
            ToggleType = NativeMenuItemToggleType.Radio
        };
        _lightThemeMenuItem.Click += (_, _) => AppThemeManager.SetTheme(AppTheme.Light);

        var themeMenu = new NativeMenu();
        themeMenu.Items.Add(_darkThemeMenuItem);
        themeMenu.Items.Add(_lightThemeMenuItem);

        var settingsMenuItem = new NativeMenuItem("Impostazioni")
        {
            Menu = themeMenu
        };

        var menu = new NativeMenu();
        menu.Items.Add(settingsMenuItem);
        NativeMenu.SetMenu(this, menu);
    }

    private void UpdateThemeMenuChecks(AppTheme theme)
    {
        if (_darkThemeMenuItem is not null)
        {
            _darkThemeMenuItem.IsChecked = theme == AppTheme.Dark;
        }

        if (_lightThemeMenuItem is not null)
        {
            _lightThemeMenuItem.IsChecked = theme == AppTheme.Light;
        }
    }
    
    private async void LoadFileAsync(object? sender, RoutedEventArgs e)
    {
        var options = new FilePickerOpenOptions
        {
            Title = "Apri un file",
            FileTypeFilter = [
                new FilePickerFileType("Text Files") { Patterns = ["*.bin"] },
                new FilePickerFileType("All Files") { Patterns = ["*"] }
            ],
            AllowMultiple = false
        };

        var result = await StorageProvider.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            if (DataContext is MainViewModel viewModel)
            {
                string decodedPath = Uri.UnescapeDataString(result.First().Path.AbsolutePath);
                viewModel.ProcessFile(decodedPath);
            } else
            {
                Console.WriteLine("Nessun file selezionato.");
            }
        }
    }
    
    private void SerialBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.CaretIndex = tb.Text?.Length ?? 0;
            tb.BringIntoView(); // forza lo scroll fino in fondo
        }
    }
}
