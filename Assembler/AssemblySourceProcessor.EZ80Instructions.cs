using Konamiman.Nestor80.Assembler.Infrastructure;

namespace Konamiman.Nestor80.Assembler
{
    public partial class AssemblySourceProcessor
    {
        // Istruzioni eZ80 che lo Z80 non ha. La maggior parte e' ereditata dallo
        // Z180; STMIX e RSMIX sono proprie dell'eZ80.
        //
        // STATO DI VERIFICA delle codifiche qui sotto:
        //  - OUT0 (n),A = ED 39 n  e' confermato da un binario prodotto da
        //    Zilog ZDS II (blink_ez80.bin: "ed 39 9c" per OUT0 (9Ch),A);
        //  - le altre seguono lo stesso schema documentato per lo Z180 ma non
        //    sono state riscontrate su un binario di riferimento.
        // Non sono ancora presenti LEA e PEA: usano la sintassi "IX+d" senza
        // parentesi, che il parser degli argomenti non riconosce, e vanno
        // affrontate insieme al resto del supporto ADL.

        static readonly string[] ExclusiveEZ80InstructionOpcodes = new[] {
            "IN0", "OUT0", "MLT", "SLP", "TST", "TSTIO",
            "OTIM", "OTIMR", "OTDM", "OTDMR",
            "STMIX", "RSMIX"
        };

        /// <summary>
        /// Istruzioni esclusive eZ80 senza argomenti o con argomenti fissi
        /// (nomi di registro).
        /// </summary>
        static readonly Dictionary<string, byte[]> FixedExclusiveEZ80Instructions = new(StringComparer.OrdinalIgnoreCase) {
            // Moltiplicazione 8x8 -> 16, Z180
            { "MLT BC", new byte[] { 0xed, 0x4c } },
            { "MLT DE", new byte[] { 0xed, 0x5c } },
            { "MLT HL", new byte[] { 0xed, 0x6c } },
            { "MLT SP", new byte[] { 0xed, 0x7c } },

            // Sleep
            { "SLP", new byte[] { 0xed, 0x76 } },

            // Trasferimenti I/O a blocchi con autodecremento del registro C, Z180
            { "OTIM",  new byte[] { 0xed, 0x83 } },
            { "OTDM",  new byte[] { 0xed, 0x8b } },
            { "OTIMR", new byte[] { 0xed, 0x93 } },
            { "OTDMR", new byte[] { 0xed, 0x9b } },

            // TST g: ED 04+g*8
            { "TST B",    new byte[] { 0xed, 0x04 } },
            { "TST C",    new byte[] { 0xed, 0x0c } },
            { "TST D",    new byte[] { 0xed, 0x14 } },
            { "TST E",    new byte[] { 0xed, 0x1c } },
            { "TST H",    new byte[] { 0xed, 0x24 } },
            { "TST L",    new byte[] { 0xed, 0x2c } },
            { "TST (HL)", new byte[] { 0xed, 0x34 } },
            { "TST A",    new byte[] { 0xed, 0x3c } },

            // Selezione del banco MBASE nelle interruzioni, proprie dell'eZ80
            { "STMIX", new byte[] { 0xed, 0x7d } },
            { "RSMIX", new byte[] { 0xed, 0x7e } },
        };

        /// <summary>
        /// Istruzioni esclusive eZ80 con un argomento variabile. Stessa forma a
        /// 5 elementi usata da Z80InstructionsWithOneVariableArgument:
        /// chiave di ricerca, tipo dell'argomento, posizione dell'argomento,
        /// byte dell'istruzione, posizione dell'argomento nei byte emessi.
        /// </summary>
        static readonly (string, CpuInstrArgType, CpuArgPos, byte[], int)[]
            ExclusiveEZ80InstructionsWithOneVariableArgument = new (string, CpuInstrArgType, CpuArgPos, byte[], int)[] {

            // IN0 g,(n): ED g*8 n
            ( "IN0 B", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x00, 0 }, 2 ),
            ( "IN0 C", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x08, 0 }, 2 ),
            ( "IN0 D", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x10, 0 }, 2 ),
            ( "IN0 E", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x18, 0 }, 2 ),
            ( "IN0 H", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x20, 0 }, 2 ),
            ( "IN0 L", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x28, 0 }, 2 ),
            ( "IN0 A", CpuInstrArgType.ByteInParenthesis, CpuArgPos.Second, new byte[] { 0xed, 0x38, 0 }, 2 ),

            // OUT0 (n),g: ED g*8+1 n   -- la variante con A e' quella confermata
            ( "OUT0 B", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x01, 0 }, 2 ),
            ( "OUT0 C", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x09, 0 }, 2 ),
            ( "OUT0 D", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x11, 0 }, 2 ),
            ( "OUT0 E", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x19, 0 }, 2 ),
            ( "OUT0 H", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x21, 0 }, 2 ),
            ( "OUT0 L", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x29, 0 }, 2 ),
            ( "OUT0 A", CpuInstrArgType.ByteInParenthesis, CpuArgPos.First, new byte[] { 0xed, 0x39, 0 }, 2 ),

            // TST n / TSTIO n
            ( "TST",   CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xed, 0x64, 0 }, 2 ),
            ( "TSTIO", CpuInstrArgType.Byte, CpuArgPos.Single, new byte[] { 0xed, 0x74, 0 }, 2 ),
        };
    }
}
