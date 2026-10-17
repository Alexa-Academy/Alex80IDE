using CommunityToolkit.Mvvm.ComponentModel;

namespace Alex80_IDE.ViewModels;

/// <summary>
/// Un flag del registro F. I flag stanno su una riga a se' nella vista registri:
/// in una cella sola venivano troncati, e la sigla da sola non dice cosa
/// significhi il flag, cosa che a un sistema pensato per chi impara serve.
/// L'eZ80 usa lo stesso registro F dello Z80, quindi la riga vale per entrambi.
/// </summary>
public sealed partial class CpuFlagViewModel : ObservableObject
{
    public CpuFlagViewModel(string name, byte mask, string description)
    {
        Name = name;
        Mask = mask;
        Description = description;
    }

    /// <summary>Sigla del flag, come compare nei manuali Z80.</summary>
    public string Name { get; }

    /// <summary>Bit del registro F che porta questo flag.</summary>
    public byte Mask { get; }

    /// <summary>Significato esteso, mostrato come suggerimento.</summary>
    public string Description { get; }

    [ObservableProperty]
    private bool _isSet;

    /// <summary>
    /// Vero quando l'ultima rilettura ha ribaltato questo bit. Vale per i flag lo
    /// stesso motivo che vale per i registri: dopo un passo di debug quello che
    /// serve sapere e' quali flag ha mosso l'istruzione appena eseguita, e un
    /// flag e' proprio il caso in cui il valore da solo non lo dice, perche' fra
    /// acceso e spento non c'e' differenza fra "cambiato" e "gia' cosi' da prima".
    /// Lo spegne il temporizzatore della vista registri.
    /// </summary>
    [ObservableProperty]
    private bool _isChanged;

    public string StateText => IsSet ? "1" : "0";

    /// <summary>Suggerimento completo: sigla, stato corrente e significato.</summary>
    public string ToolTipText => $"{Name} = {StateText} — {Description}";

    /// <summary>
    /// Rilegge il proprio bit dal registro F appena arrivato dalla scheda e dice se
    /// e' cambiato. Con <paramref name="highlight"/> spento aggiorna senza accendere
    /// l'evidenza: serve alla prima lettura, dove non c'e' un passo precedente.
    /// </summary>
    public bool Refresh(byte f, bool highlight)
    {
        bool isSet = (f & Mask) != 0;

        if (isSet == IsSet)
        {
            return false;
        }

        IsSet = isSet;

        if (highlight)
        {
            IsChanged = true;
        }

        return true;
    }

    partial void OnIsSetChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ToolTipText));
    }
}
