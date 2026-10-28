using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Alex80_IDE.InstructionReference;

public static class Ez80Timing
{
    public const string Manual = "https://www.zilog.com/docs/UM0077.pdf";
    public sealed record Entry(string Cycles, string Url);
    private sealed record Row(string Name, string Operand, string Mode, string Cycles, int Page, string Opcodes, Regex Match);
    private static readonly IReadOnlyList<Row> Rows = Load();
    private static IReadOnlyList<Row> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Alex80_IDE.InstructionReference.Ez80Timings.tsv")!;
        using var reader = new StreamReader(stream);
        var rows = new List<Row>();
        while (reader.ReadLine() is { } line)
        {
            if (line.StartsWith('#')) continue;
            var f = line.Split('\t'); if (f.Length != 6) continue;
            rows.Add(new(f[0], f[1], f[2], f[3], int.Parse(f[4]), f[5], OperandRegex(f[1])));
        }
        return rows;
    }
    private static Regex OperandRegex(string pattern)
    {
        pattern = pattern.Replace(" ", "").Replace("—", "").Replace("’", "").Replace("'", "");
        var parts = Regex.Matches(pattern, "[A-Za-z]+|."); var b = new StringBuilder("^");
        foreach (Match part in parts)
        {
            b.Append(part.Value switch
            {
                "r" => "(?:A|B|C|D|E|H|L)", "rr" or "ss" or "dd" or "qq" => "(?:BC|DE|HL|SP|AF)",
                "ir" => "(?:IXH|IXL|IYH|IYL)", "cc" => "(?:NZ|Z|NC|C|PO|PE|P|M)",
                "n" or "nn" or "mn" or "Mmn" => "[0-9A-F]+h", "b" => "[0-7]",
                "d" => "[0-9A-F]+h", _ => Regex.Escape(part.Value)
            });
        }
        // The manual writes IX+d even for a negative displacement.
        return new Regex(b.Append('$').ToString().Replace("\\+", "[+-]"), RegexOptions.CultureInvariant);
    }
    public sealed record Specific(string Mnemonic, int Length);
    public static Specific? TrySpecific(IReadOnlyList<byte> bytes, bool adl)
    {
        foreach (var row in Rows)
        {
            if (row.Mode != "X" && row.Mode != "0/1" && row.Mode != (adl ? "1" : "0")) continue;
            var tokens = row.Opcodes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length < 2 || tokens[0] is not ("ED" or "DD" or "FD")) continue;
            if (Regex.IsMatch(row.Operand, @"\b(r|rr|cc|b|n|mn|Mmn|ir)\b")) continue;
            if (tokens.Any(t => t != "dd" && !Regex.IsMatch(t, "^[0-9A-F]{2}$"))) continue;
            if (bytes.Count < tokens.Length) continue;
            bool match = true; int? disp = null;
            for (int i = 0; i < tokens.Length; i++)
                if (tokens[i] == "dd") disp = (sbyte)bytes[i];
                else if (Convert.ToByte(tokens[i], 16) != bytes[i]) match = false;
            if (!match) continue;
            string operand = row.Operand.Replace(" ", "").Replace("—", "");
            if (disp is { } d) operand = operand.Replace("+d", $"{(d < 0 ? "-" : "+")}{Math.Abs(d):X2}h");
            return new(row.Name + (operand == "" ? "" : " " + operand), tokens.Length);
        }
        return null;
    }
    public static Entry? Find(string mnemonic, byte[] bytes, bool adl)
    {
        if (mnemonic == "NOP") return new("1", Manual + "#search=NOP");
        var split = mnemonic.Split(' ', 2); var name = split[0];
        string operand = split.Length > 1 ? split[1].Replace(" ", "").Replace("(C)", "(BC)").Replace("'", "").Replace("’", "") : "";
        if (name == "TST" && !operand.StartsWith("A,")) operand = "A," + operand;
        var candidates = Rows.Where(r => r.Name == name && (r.Mode is "X" or "0/1" || r.Mode == (adl ? "1" : "0")) && r.Match.IsMatch(operand));
        foreach (var row in candidates.OrderByDescending(r => r.Opcodes.Count(char.IsDigit)))
        {
            // Match literal opcode bytes too: similar operands may describe
            // different instructions (I/R/MB) or different mode prefixes.
            var tokens = row.Opcodes.Split(',', StringSplitOptions.TrimEntries);
            bool valid = true;
            for (int i = 0; i < tokens.Length && i < bytes.Length; i++)
                if (Regex.IsMatch(tokens[i], "^[0-9A-F]{2}$") && Convert.ToByte(tokens[i], 16) != bytes[i]) valid = false;
            if (!valid) continue;
            string cycles = row.Cycles;
            if (row.Mode == "0/1" && cycles.Contains('/')) cycles = cycles.Split('/')[adl ? 1 : 0];
            if (row.Opcodes.Contains('*') || row.Opcodes.Contains('+')) cycles += " (base; totale dipendente dalle ripetizioni)";
            return new(cycles, Manual + "#page=" + (row.Page + 9));
        }
        return null;
    }
}
