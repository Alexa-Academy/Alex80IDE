namespace Konamiman.Nestor80.Assembler.Output
{
    /// <summary>
    /// Riga "ASSUME ADL = n" dell'assembler eZ80. Dichiara se il codice che
    /// segue gira in modalita' ADL (indirizzi a 24 bit) oppure in modalita'
    /// Z80 compatibile (16 bit).
    /// </summary>
    public class AssumeAdlLine : ProcessedSourceLine
    {
        public bool AdlMode { get; set; }

        public override string ToString() => $"{base.ToString()}, ADL={(AdlMode ? 1 : 0)}";
    }
}
