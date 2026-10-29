using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Alex80_IDE.Controls;

/// <summary>
/// Controlli nella barra globale: da dove arrivano clock e cicli di memoria,
/// e a che frequenza.
/// </summary>
public partial class BoardToolbarView : UserControl
{
    public BoardToolbarView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Invio conferma la frequenza. Il campo è legato con UpdateSourceTrigger=LostFocus,
    /// perché ogni valore confermato viene mandato alla scheda e il firmware, per
    /// applicarlo, ferma e riavvia il clock: a ogni tasto premuto la CPU si fermerebbe
    /// una volta per cifra. Togliere il fuoco è il modo che Avalonia offre per confermare
    /// un binding di questo tipo senza toccarne l'espressione.
    /// </summary>
    private void OnClockFrequencyKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Return))
        {
            return;
        }

        (this.GetVisualRoot() as TopLevel)?.FocusManager?.ClearFocus();
        e.Handled = true;
    }
}
