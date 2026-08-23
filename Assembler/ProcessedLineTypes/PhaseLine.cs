namespace Konamiman.Nestor80.Assembler.Output
{
    public class PhaseLine : ProcessedSourceLine, IChangesLocationCounter
    {
        public AddressType NewLocationArea { get; set; }
        public uint NewLocationCounter { get; set; }

        public override string ToString() => $"{base.ToString()} {NewLocationCounter:X4}";
    }
}
