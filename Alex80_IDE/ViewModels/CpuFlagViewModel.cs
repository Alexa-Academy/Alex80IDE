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

    public string StateText => IsSet ? "1" : "0";

    /// <summary>Suggerimento completo: sigla, stato corrente e significato.</summary>
    public string ToolTipText => $"{Name} = {StateText} — {Description}";

    /// <summary>Rilegge il proprio bit dal registro F appena arrivato dalla scheda.</summary>
    public void Update(byte f) => IsSet = (f & Mask) != 0;

    partial void OnIsSetChanged(bool value)
    {
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(ToolTipText));
    }
}
