using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Alex80_IDE.InstructionReference;

// The bookmark destinations point to the start of the instruction description,
// even when its timing table continues on a later page.
public static class InstructionManual
{
    private sealed record Page(bool Ez80, string Name, int Number, Regex Operand, int Specificity);
    private static readonly IReadOnlyList<Page> Pages = Load();
    private const string ByteRegister = "(?:A|B|C|D|E|H|L)";
    private const string Immediate = "(?:[0-9A-F]+h|[0-9]+)";
    private const string ByteOperand = "(?:A|B|C|D|E|H|L|\\(HL\\)|\\(I[XY][+-][0-9A-F]+h\\)|[0-9A-F]+h)";

    public static string UrlFor(string mnemonic, bool ez80)
    {
        var (name, operand) = Split(mnemonic);
        // The eZ80 manual spells out A for these otherwise implicit operands.
        if (ez80 && (name is "AND" or "OR" or "XOR" or "SUB" or "CP" or "TST") && !operand.StartsWith("A,"))
            operand = "A," + operand;
        var page = Pages.Where(p => p.Ez80 == ez80 && p.Name == name && p.Operand.IsMatch(operand))
            .OrderByDescending(p => p.Specificity).FirstOrDefault();
        var manual = ez80 ? Ez80Timing.Manual : InstructionDecoder.Z80Manual;
        return page is null ? manual : manual + "#page=" + page.Number;
    }

    private static (string Name, string Operand) Split(string mnemonic)
    {
        var parts = mnemonic.Split(' ', 2);
        return (parts[0].Split('.')[0].ToUpperInvariant(), parts.Length > 1 ? Normalize(parts[1]) : "");
    }
    private static string Normalize(string operand) => operand.Replace(" ", "")
        .Replace("'", "").Replace("’", "").Replace("′", "").Replace("(C)", "(BC)");

    private static IReadOnlyList<Page> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Alex80_IDE.InstructionReference.ManualPages.tsv")!;
        using var reader = new StreamReader(stream);
        var pages = new List<Page>();
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('#')) continue;
            var fields = line.Split('\t');
            if (fields.Length != 3) continue;
            var (name, operand) = Split(fields[1]);
            var pattern = new StringBuilder("^");
            int specificity = 0;
            foreach (Match token in Regex.Matches(operand, "IX/Y|[A-Za-z]+|."))
            {
                string? variable = token.Value switch
                {
                    "r" => ByteRegister,
                    "s" or "m" => ByteOperand,
                    "dd" or "ss" or "qq" or "pp" or "rr" or "rxy" => "(?:BC|DE|HL|SP|AF|IX|IY)",
                    "ir" => "(?:IXH|IXL|IYH|IYL)",
                    "cc" => "(?:NZ|Z|NC|C|PO|PE|P|M)",
                    "n" or "nn" or "Mmn" or "e" or "d" or "p" => Immediate,
                    "b" => "[0-7]",
                    "IX/Y" => "(?:IX|IY)",
                    "+" => "[+-]",
                    _ => null
                };
                pattern.Append(variable ?? Regex.Escape(token.Value));
                // Prefer LD A,(BC) over LD r,r, and INC IX over INC ss.
                if (variable is null) specificity += token.Length;
                else if (token.Value == "IX/Y") specificity += 2;
            }
            pages.Add(new(fields[0] == "eZ80", name, int.Parse(fields[2]),
                new Regex(pattern.Append('$').ToString(), RegexOptions.CultureInvariant), specificity));
        }
        return pages;
    }
}
