using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alex80_IDE.ViewModels;

/// <summary>Bordo della finestra su cui e' agganciato un gruppo di tool window.</summary>
public enum ToolDockPlacement
{
    Left,
    Right,
    Bottom
}

/// <summary>
/// Gruppo di tool window agganciate a un bordo della finestra, nello stile di Rider o
/// Android Studio: una strip di icone sempre visibile e un solo pannello aperto per volta.
/// Un dock senza strumenti sparisce del tutto, strip compresa: spostare uno strumento da
/// un bordo all'altro e' quindi solo una riga in MainViewModel.Layout.
/// </summary>
public sealed partial class ToolDockViewModel : ObservableObject
{
    /// <summary>Misura usata quando il dock non ha nessuno strumento a cui chiederla.</summary>
    private const double FallbackSize = 300;

    public ToolDockViewModel(ToolDockPlacement placement, params ToolWindowViewModel[] tools)
    {
        Placement = placement;

        Tools = new ObservableCollection<ToolWindowViewModel>(tools);

        foreach (var tool in Tools)
        {
            tool.Dock = this;
        }

        _selectedTool = Tools.FirstOrDefault();
        SyncActiveStates();
    }

    public ToolDockPlacement Placement { get; }

    public IReadOnlyList<ToolWindowViewModel> Tools { get; }

    /// <summary>Un dock senza strumenti non ha nulla da mostrare, nemmeno la strip.</summary>
    public bool HasTools => Tools.Count > 0;

    [ObservableProperty]
    private ToolWindowViewModel? _selectedTool;

    [ObservableProperty]
    private bool _isOpen = true;

    /// <summary>Il pannello occupa spazio solo se c'e' uno strumento aperto da mostrare.</summary>
    public bool IsPanelVisible => IsOpen && HasTools;

    /// <summary>
    /// Misura del pannello: e' quella dello strumento mostrato, cosi' ogni tool window
    /// conserva lo spazio che le serve.
    /// </summary>
    public double PanelSize
    {
        get => SelectedTool?.PanelSize ?? FallbackSize;
        set
        {
            if (SelectedTool is not { } tool || tool.PanelSize.Equals(value))
            {
                return;
            }

            tool.PanelSize = value;
            OnPropertyChanged();
        }
    }

    /// <summary>Titolo dell'intestazione del pannello: quello della tool window mostrata.</summary>
    public string HeaderTitle => SelectedTool?.Title ?? string.Empty;

    /// <summary>
    /// Porta in primo piano la tool window indicata; se e' gia' quella visibile chiude il
    /// pannello, come fa il clic sull'icona gia' selezionata negli IDE JetBrains.
    /// </summary>
    public void Toggle(ToolWindowViewModel tool)
    {
        if (!tool.IsAvailable)
        {
            return;
        }

        if (IsOpen && ReferenceEquals(SelectedTool, tool))
        {
            IsOpen = false;
            return;
        }

        SelectedTool = tool;
        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    /// <summary>Chiude il pannello se sta mostrando proprio lo strumento indicato.</summary>
    internal void CloseIfShowing(ToolWindowViewModel tool)
    {
        if (IsOpen && ReferenceEquals(SelectedTool, tool))
        {
            IsOpen = false;
        }
    }

    /// <summary>Ripristina lo stato salvato nelle impostazioni.</summary>
    public void Restore(bool isOpen, string? selectedToolId, IReadOnlyDictionary<string, double>? toolSizes)
    {
        if (toolSizes is not null)
        {
            foreach (var tool in Tools)
            {
                if (toolSizes.TryGetValue(tool.Id, out var size))
                {
                    // Una misura fuori scala (finestra molto piu' piccola, file rovinato)
                    // tornerebbe come pannello inutilizzabile: meglio il valore predefinito.
                    tool.PanelSize = size is >= 120 and <= 2000 ? size : tool.DefaultSize;
                }
            }
        }

        if (selectedToolId is not null &&
            Tools.FirstOrDefault(tool => tool.Id == selectedToolId) is { } selected)
        {
            SelectedTool = selected;
        }

        IsOpen = isOpen;
    }

    /// <summary>Misure correnti degli strumenti, per il salvataggio nelle impostazioni.</summary>
    public Dictionary<string, double> ToolSizes() =>
        Tools.ToDictionary(tool => tool.Id, tool => tool.PanelSize);

    partial void OnIsOpenChanged(bool value)
    {
        OnPropertyChanged(nameof(IsPanelVisible));
        SyncActiveStates();
    }

    partial void OnSelectedToolChanged(ToolWindowViewModel? value)
    {
        OnPropertyChanged(nameof(HeaderTitle));
        OnPropertyChanged(nameof(PanelSize));
        SyncActiveStates();
    }

    private void SyncActiveStates()
    {
        foreach (var tool in Tools)
        {
            tool.IsActive = IsOpen && ReferenceEquals(tool, SelectedTool);
        }
    }
}
