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
    public bool IsInjected { get; }
    public bool IsReset { get; }
    public bool IsManualPulse { get; }
    // I segnali del bus sono attivi bassi. RFSH alto esclude il refresh.
    public bool IsInstructionFetch => !IsInjected && !IsReset && !Z80_M1 &&
                                      !Z80_MREQ && !Z80_RD && Z80_RFSH;
    public uint? ClockNumber { get; }
    public bool? Waiting { get; }
    public bool? StartsInstruction { get; }
    public bool? FullTrace { get; }
    public bool? HardwareHalt { get; }
    public string ClockText => ClockNumber?.ToString() ?? "—";
    [ObservableProperty] private string _tStateText = "—";
    [ObservableProperty] private string _observedPhase = string.Empty;
    partial void OnObservedPhaseChanged(string value) => OnPropertyChanged(nameof(TracePhase));
    public string TracePhase => IsReset ? "Reset" : IsInjected ? "Lettura registri" : HardwareHalt == true ? "HALT" : Waiting == true ? "WAIT debugger" : ObservedPhase;

    [ObservableProperty] 
    private string _mnemonic;
    
    
    // Proprietà formattate per la GUI
    public string AddressHex => Address.ToString("X4");
    public string DataHex => Data.ToString("X2");
    
    public LogElement(ushort address, byte data, bool z80_rd, bool z80_wr, bool z80_iorq, bool z80_mreq, bool z80_rfsh, bool z80_m1, string mnemonic, bool injected = false, bool reset = false, uint? clockNumber = null, byte? traceFlags = null, bool manualPulse = false)
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
        IsInjected = injected;
        IsReset = reset;
        IsManualPulse = manualPulse;
        ClockNumber = clockNumber;
        Waiting = traceFlags is { } flags ? (flags & 1) != 0 : null;
        FullTrace = traceFlags is { } mode ? (mode & 2) != 0 : null;
        HardwareHalt = traceFlags is { } halt ? (halt & 8) != 0 : null;
        StartsInstruction = traceFlags is { } start ? (start & 4) != 0 : null;
    }
    
}

