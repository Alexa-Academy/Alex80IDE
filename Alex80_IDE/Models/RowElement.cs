using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace Alex80_IDE.Models;

public partial class RowElement : ObservableObject
{
    // uint e non ushort: su eZ80 la RAM sta a 0xFFF000, fuori dai 64 KB.
    [ObservableProperty]
    private uint _address;

    [ObservableProperty]
    private List<byte> _dataArray;

    [ObservableProperty]
    private string _ascii;

    /// <summary>
    /// Larghezza dell'indirizzo decisa alla creazione della riga. Il testo e'
    /// precalcolato invece che affidato a un converter perche' la larghezza
    /// dipende dalla modalita' della scheda, che un IValueConverter senza
    /// stato non puo' conoscere.
    /// </summary>
    public bool WideAddress { get; }

    public string AddressText => Address.ToString(WideAddress ? "X6" : "X4");

    partial void OnAddressChanged(uint value) => OnPropertyChanged(nameof(AddressText));

    public RowElement(uint address, List<byte> dataArray, string ascii, bool wideAddress = false)
    {
        WideAddress = wideAddress;
        Address = address;
        DataArray = dataArray;
        Ascii = ascii;
    }
}
