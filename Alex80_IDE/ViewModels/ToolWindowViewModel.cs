using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alex80_IDE.ViewModels;

/// <summary>
/// Una tool window: il pannello degli strumenti (registri, memoria, seriale...) con il
/// pulsante che lo richiama dalla strip di bordo. Il contenuto vero e proprio resta
/// dichiarato nella finestra: qui c'e' solo quello che serve alla strip e allo stato.
/// </summary>
public sealed partial class ToolWindowViewModel : ObservableObject
{
    public ToolWindowViewModel(string id, string title, string iconKey, double defaultSize)
    {
        Id = id;
        Title = title;
        IconKey = iconKey;
        DefaultSize = defaultSize;
        _panelSize = defaultSize;
    }

    /// <summary>Identificatore stabile: finisce nelle impostazioni, non tradurlo.</summary>
    public string Id { get; }

    /// <summary>Titolo mostrato nell'intestazione del pannello e nel suggerimento della strip.</summary>
    public string Title { get; }

    /// <summary>Chiave della StreamGeometry usata come icona nella strip.</summary>
    public string IconKey { get; }

    /// <summary>Misura del pannello quando non c'e' nulla di salvato.</summary>
    public double DefaultSize { get; }

    /// <summary>
    /// Larghezza (dock laterale) o altezza (dock inferiore) del pannello quando mostra
    /// questa tool window. Ogni strumento ha la sua: il dump della memoria vuole molto
    /// piu' spazio dei registri, e passare dall'uno all'altro non deve far perdere la
    /// misura scelta per il primo.
    /// </summary>
    [ObservableProperty]
    private double _panelSize;

    /// <summary>Dock che la ospita. Lo imposta il dock stesso quando la prende in carico.</summary>
    internal ToolDockViewModel? Dock { get; set; }

    /// <summary>Vero quando il pannello e' aperto proprio su questa tool window.</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>Apre il pannello su questa tool window, oppure lo chiude se e' gia' quella mostrata.</summary>
    [RelayCommand]
    private void Toggle() => Dock?.Toggle(this);
}
