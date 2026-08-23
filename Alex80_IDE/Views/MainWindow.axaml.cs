using System;
using System.Collections.Generic;
using System.Linq;
using Alex80_IDE.Helpers;
using Alex80_IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Alex80_IDE;

public partial class MainWindow : Window
{
    private readonly List<ToolDockLayout> _dockLayouts = new();
    private EditorWindow? _editorWindow;
    private NativeMenuItem? _darkThemeMenuItem;
    private NativeMenuItem? _lightThemeMenuItem;
    private double _normalWindowWidth;
    private double _normalWindowHeight;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowSize();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        ConfigureSystemMenu();
        AppShortcuts.Install(this);
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
        SaveDockState(settings);
        settings.Save();
    }

    /// <summary>
    /// Aggancia i dock alle righe e colonne che li ospitano. Il collegamento non puo' essere
    /// un binding: le definizioni della griglia non fanno parte dell'albero logico e non
    /// ereditano quindi il DataContext.
    /// </summary>
    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        foreach (var layout in _dockLayouts)
        {
            layout.Dispose();
        }

        _dockLayouts.Clear();

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        RestoreDockState(viewModel, UserSettings.Load());

        // Le definizioni di griglia non sono controlli e non ricevono un campo da x:Name:
        // vanno prese per posizione. Gli indici sono annotati anche in MainWindow.axaml.
        _dockLayouts.Add(ToolDockLayout.ForColumn(viewModel.LeftDock, CenterGrid.ColumnDefinitions[0]));
        _dockLayouts.Add(ToolDockLayout.ForColumn(viewModel.RightDock, CenterGrid.ColumnDefinitions[4]));
        _dockLayouts.Add(ToolDockLayout.ForRow(viewModel.BottomDock, WorkAreaGrid.RowDefinitions[2]));
    }

    private static void RestoreDockState(MainViewModel viewModel, UserSettings settings)
    {
        foreach (var dock in viewModel.Docks)
        {
            if (settings.Docks.TryGetValue(dock.Placement.ToString(), out var saved))
            {
                dock.Restore(saved.IsOpen, saved.SelectedTool, saved.ToolSizes);
            }
        }
    }

    private void SaveDockState(UserSettings settings)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        foreach (var dock in viewModel.Docks)
        {
            settings.Docks[dock.Placement.ToString()] = new ToolDockSettings
            {
                IsOpen = dock.IsOpen,
                SelectedTool = dock.SelectedTool?.Id,
                ToolSizes = dock.ToolSizes()
            };
        }
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
        _editorWindow.Closed += (_, _) => _editorWindow = null;
        _editorWindow.Show(this);
    }

    private void OnThemeChanged(AppTheme theme)
    {
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
}
