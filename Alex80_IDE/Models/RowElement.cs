using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Generic;

namespace Alex80_IDE.Models;

public partial class RowElement : ObservableObject
{
    [ObservableProperty]
    private ushort _address;

    [ObservableProperty]
    private List<byte> _dataArray;

    [ObservableProperty]
    private string _ascii;

    public RowElement(ushort address, List<byte> dataArray, string ascii)
    {
        Address = address;
        DataArray = dataArray;
        Ascii = ascii;
    }
}