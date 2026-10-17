using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Alex80_IDE.ViewModels;

/// <summary>
/// Un registro della CPU nella vista registri. Oltre al valore porta con se' il
/// fatto di essere appena cambiato: in una sessione di debug si avanza
/// un'istruzione alla volta, e quello che serve vedere e' cosa ha toccato
/// l'istruzione appena eseguita, non l'elenco di tutti i registri uguale a prima.
/// L'evidenza si spegne da sola dopo qualche secondo, cosi' resta una segnalazione
/// del passo appena fatto e non uno stato che si accumula.
/// </summary>
public sealed partial class CpuRegisterViewModel : ObservableObject
{
    private readonly Func<string> _read;

    public CpuRegisterViewModel(string name, string description, Func<string> read)
    {
        Name = name;
        Description = description;
        _read = read;
        _valueText = read();
    }

    /// <summary>Sigla del registro, come compare nei manuali Z80.</summary>
    public string Name { get; }

    /// <summary>Significato del registro, mostrato come suggerimento.</summary>
    public string Description { get; }

    [ObservableProperty]
    private string _valueText;

    /// <summary>
    /// Vero quando l'ultima rilettura ha portato un valore diverso dal precedente.
    /// Lo spegne il temporizzatore della vista registri.
    /// </summary>
    [ObservableProperty]
    private bool _isChanged;

    /// <summary>Suggerimento completo: sigla, valore corrente e significato.</summary>
    public string ToolTipText => $"{Name} = {ValueText} — {Description}";

    partial void OnValueTextChanged(string value) => OnPropertyChanged(nameof(ToolTipText));

    /// <summary>
    /// Rilegge il valore dal modello e dice se e' cambiato. Con
    /// <paramref name="highlight"/> spento il valore viene aggiornato senza
    /// accendere l'evidenza: serve alla prima lettura, quando non c'e' un valore
    /// di prima con cui confrontarsi, e ai cambi di formato fra Z80 ed eZ80, dove
    /// a cambiare e' il numero di cifre e non il contenuto del registro.
    /// </summary>
    public bool Refresh(bool highlight)
    {
        string value = _read();

        if (string.Equals(ValueText, value, StringComparison.Ordinal))
        {
            return false;
        }

        ValueText = value;

        if (highlight)
        {
            IsChanged = true;
        }

        return true;
    }
}
