using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.VisualBasic;

namespace Alex80_IDE.Models;

public partial class LogElement : ObservableObject
{
    public ushort Address { get; set; }
    public byte Data { get; set; }
    public bool Z80_RD { get; set; }
    public bool Z80_WR { get; set; }
    public bool Z80_IORQ { get; set; }
    public bool Z80_MREQ { get; set; }
    public bool Z80_RFSH { get; set; }
    public bool Z80_M1 { get; set; }

    [ObservableProperty] 
    private string _mnemonic;
    
    
    // Proprietà formattate per la GUI
    public string AddressHex => Address.ToString("X4");
    public string DataHex => Data.ToString("X2");
    
    public LogElement(ushort address, byte data, bool z80_rd, bool z80_wr, bool z80_iorq, bool z80_mreq, bool z80_rfsh, bool z80_m1, string mnemonic)
    {
        Address = address;
        Data = data;
        Z80_RD = z80_rd;
        Z80_WR = z80_wr;
        Z80_IORQ = z80_iorq;
        Z80_MREQ = z80_mreq;
        Z80_RFSH = z80_rfsh;
        Z80_M1 = z80_m1;
        Mnemonic = mnemonic;
    }
    
}

