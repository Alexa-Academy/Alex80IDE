namespace Konamiman.Nestor80.Assembler.Infrastructure
{
    public enum CpuType
    {
        Unknown,
        Z80,
        R800,
        Z280,

        /// <summary>
        /// Zilog eZ80. Sovrainsieme dello Z180: aggiunge IN0/OUT0/MLT/TST/SLP e
        /// la modalita' ADL con indirizzi a 24 bit (vedi ASSUME ADL).
        /// </summary>
        EZ80
    }
}
