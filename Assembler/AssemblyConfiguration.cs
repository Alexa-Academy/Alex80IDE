using System.Text;

namespace Konamiman.Nestor80.Assembler
{
    /// <summary>
    /// Assembly process configuration object. An instance of this object needs
    /// to be passed to <see cref="AssemblySourceProcessor.Assemble(Stream, Encoding, AssemblyConfiguration)"/>.
    /// </summary>
    public class AssemblyConfiguration
    {
        /// <summary>
        /// Name of the encoding to use to convert strings to bytes in DEFB instructions.
        /// This encoding can also be changed in code by using the .STRENC instruction.
        /// </summary>
        public string OutputStringEncoding { get; init; } = "ASCII";

        /// <summary>
        /// Build type: absolute, relocatable or automatic (select based on the code itself:
        /// will be absolute if ORG is found before a CPU instruction or any of these:
        /// CSEG, DSEG, COMMON, DB, DW, DS, DC, DM, DZ, PUBLIC, EXTRN, .REQUEST; otherwise
        /// it will be relocatable).
        /// </summary>
        public BuildType BuildType { get; init; } = BuildType.Automatic;

        /// <summary>
        /// Name of the target CPU. This can also be changed in code with the .CPU instruction.
        /// </summary>
        public string CpuName { get; init; } = "Z80";

        /// <summary>
        /// Allow escape sequences in strings or not (needs to be disabled for old
        /// code that contains literal "\" characters in strings).
        /// </summary>
        public bool AllowEscapesInStrings { get; init; } = true;

        /// <summary>
        /// Callback to use for INCLUDE instructions, the parameter is the name of
        /// the file requested and the return value is a stream to read source code from.
        /// </summary>
        public Func<string, Stream> GetStreamForInclude { get; init; } = _ => null;

        /// <summary>
        /// Callback to use for INCBIN instructions, the parameter is the name of
        /// the file requested and the return value is a stream to read source code from.
        /// </summary>
        public Func<string, Stream> GetStreamForIncbin { get; init; } = _ => null;

        /// <summary>
        /// List of predefined symbols as pairs of name-value, they will be registerd
        /// as if they were defined with DEFL.
        /// </summary>

        public (string, ushort)[] PredefinedSymbols = Array.Empty<(string, ushort)>();

        /// <summary>
        /// Maximum number of allowed assembly errors, assembly process will stop if reached;
        /// 0 means "infinite".
        /// </summary>
        public int MaxErrors { get; init; } = 0;

        /// <summary>
        /// Allow or not bare expressions in code, these are treated as DEFB statements;
        /// e.g. FOO: 1,2,3 qill be treated as FOO: DEFB 1,2,3
        /// </summary>
        public bool AllowBareExpressions { get; init; } = false;

        /// <summary>
        /// Allow or not relative labels (they start wiht a dot and are relative
        /// to the last non-relative label).
        /// </summary>
        public bool AllowRelativeLabels { get; init; } = false;

        /// <summary>
        /// Maximum amount of content that will be read from files included with the INCBIN instruction.
        /// </summary>
        public int MaxIncbinFileSize { get; init; } = 65536;

        /// <summary>
        /// True to produce relocatable files that are compatible with LINK-80
        /// (so public and external symbols are limited to 6 ASCII-only characters, and the set of
        /// arithmetic operators allowed for expressions with external references is limited).
        /// </summary>
        public bool Link80Compatibility { get; init; } = false;

        /// <summary>
        /// True if a hash character (#) present at the beginning of an expression needs to be discarded
        /// before evaluating the expression. This is useful for assembling sources intended for the
        /// SDAS assembler, which expects numeric constants to be prefixed with a hash character.
        /// </summary>
        public bool DiscardHashPrefix { get; init; } = false;

        /// <summary>
        /// True if instruction aliases with a dot prefix (e.g. ".DS" as an alias of "DS") are accepted.
        /// </summary>
        public bool AcceptDottedInstructionAliases { get; set; } = false;

        /// <summary>
        /// True to consider symbols that are unknown in pass 2 as external symbol references.
        /// </summary>
        public bool TreatUnknownSymbolsAsExternals { get; set; } = false;

        /// <summary>
        /// True to accept the syntax variations used by the TASM assembler, in addition to
        /// (not instead of) the regular Macro80-compatible syntax. Enabling this implies:
        /// <list type="bullet">
        /// <item>Instruction aliases with a dot prefix are accepted (same as <see cref="AcceptDottedInstructionAliases"/>),
        /// e.g. ".EQU", ".ORG", ".END", ".INCLUDE".</item>
        /// <item>Labels placed at the very beginning of a line (column 1) don't need to be
        /// terminated with a colon.</item>
        /// <item>The "$" prefix introduces a hexadecimal number when it's followed by at least one
        /// hexadecimal digit, e.g. "$8000" (a lone "$" still means "current location pointer").</item>
        /// <item>The C-style bitwise operators "&amp;" (AND), "|" (OR), "^" (XOR), "~" (NOT),
        /// "&lt;&lt;" (SHL) and "&gt;&gt;" (SHR) are accepted.</item>
        /// </list>
        /// The TASM-specific pseudo-operators (.BYTE, .WORD, .TEXT, .BLOCK, .FILL) are always
        /// available, regardless of the value of this property.
        /// </summary>
        public bool TasmCompatibility { get; set; } = false;

        /// <summary>
        /// The value of the location counter at the beginning of the assembly process,
        /// used only when the build type is absolute (an ORG instruction in the source
        /// still overrides it). Useful to assemble a source that has no ORG of its own
        /// as a chunk that goes to a given memory address.
        /// </summary>
        public ushort StartAddress { get; set; } = 0;

        /// <summary>
        /// True to ignore the END instruction when it's found inside an INCLUDEd file,
        /// so that it ends that file only and the assembly process continues with the
        /// remaining files. Useful to assemble as a single unit a set of files that
        /// were originally written as independent programs.
        /// </summary>
        public bool IgnoreEndInstructionInIncludedFiles { get; set; } = false;

        /// <summary>
        /// Character sequence to use as end of line markers for text files.
        /// </summary>
        public string EndOfLine { get; set; } = Environment.NewLine;
    }
}
