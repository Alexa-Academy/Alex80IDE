using System;
using System.Collections.Generic;
using System.Linq;
using Alex80_IDE.Helpers;
using Alex80_IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace Alex80_IDE;

public partial class MainWindow : Window
{
    private readonly List<ToolDockLayout> _dockLayouts = new();
    private EditorWindow? _editorWindow;
    private double _normalWindowWidth;
    private double _normalWindowHeight;

    public MainWindow()
    {
        InitializeComponent();
        RestoreWindowSize();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnWindowSizeChanged;
        Closing += OnWindowClosing;
        AppMenu.Install(this);
        AppShortcuts.Install(this);
        InstallProjectFilesDrop();
    }

    /// <summary>
    /// Trascinare dei sorgenti sull'elenco li aggiunge al progetto: e' la stessa cosa che
    /// fa "Aggiungi file" nel menu contestuale, per chi arriva dal gestore di file.
    /// </summary>
    private void InstallProjectFilesDrop()
    {
        ProjectFilesList.AddHandler(DragDrop.DragOverEvent, OnProjectFilesDragOver);
        ProjectFilesList.AddHandler(DragDrop.DropEvent, OnProjectFilesDrop);
    }

    private void OnProjectFilesDragOver(object? sender, DragEventArgs e)
    {
        // Senza un progetto aperto non c'e' dove mettere i file: il cursore lo dice subito.
        var accepted = DataContext is MainViewModel { HasProject: true } && DroppedPaths(e).Count > 0;

        e.DragEffects = accepted ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnProjectFilesDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var paths = DroppedPaths(e);

        if (paths.Count > 0)
        {
            viewModel.AddFilesToProject(paths);
        }
    }

    /// <summary>Percorsi locali dei file trascinati. Quel che non e' un file locale viene ignorato.</summary>
    private static List<string> DroppedPaths(DragEventArgs e) =>
        e.Data.GetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path!)
            .ToList() ?? [];

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
}
