using System;

namespace Alex80_IDE.InstructionReference;

public sealed record InstructionInfo(
    uint Address, string Mnemonic, byte[] Bytes, string Operation, string Description,
    string Flags, string Timing, string MachineCycles, int? NominalTStates,
    string TimingNote, string ManualUrl, bool Complete = true)
{
    public int AddressDigits { get; init; } = 4;
    public int? TakenTStates { get; init; }
    public int? NotTakenTStates { get; init; }
    public string OpcodeText => string.Join(" ", Array.ConvertAll(Bytes, b => b.ToString("X2")));
    public string Header => $"{Address.ToString(AddressDigits == 6 ? "X6" : "X4")} · {Mnemonic}";
    public string SizeText => Complete ? $"{Bytes.Length} byte" : "Lunghezza non determinata";
    public string ManualButtonText
    {
        get
        {
            const string fragment = "#page=";
            int start = ManualUrl.IndexOf(fragment, StringComparison.Ordinal);
            return start >= 0 ? $"Apri manuale · pagina PDF {ManualUrl[(start + fragment.Length)..]}" : "Apri manuale";
        }
    }
    public string IdealTime(int frequency) => NominalTStates is { } t && frequency > 0
        ? $"{1000.0 * t / frequency:0.###} ms teorici a {frequency} Hz, senza WAIT" : string.Empty;
}
