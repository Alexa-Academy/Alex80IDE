using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace Alex80_IDE.Services;

/// <summary>Indirizzi dei byte emessi -> righe del listing MACRO-80 generato dall'IDE.</summary>
public static class ListingAddressMap
{
    // Configurazione predefinita: indirizzo, quattro spazi, area byte da 15 caratteri.
    // Richiedere byte reali esclude ORG, EQU, etichette, intestazioni e simboli.
    private static readonly Regex OutputRow = new(
        @"^  (?<address>[0-9A-Fa-f]{4,6}) {4}(?<bytes>.{15})", RegexOptions.Compiled);

    public static IReadOnlyDictionary<uint, int> Create(string listing)
    {
        var lines = new Dictionary<uint, int>();
        using var reader = new StringReader(listing);
        string? text;
        var lineNumber = 0;
        while ((text = reader.ReadLine()) is not null)
        {
            lineNumber++;
            var match = OutputRow.Match(text);
            if (!match.Success) continue;

            var address = uint.Parse(match.Groups["address"].Value, NumberStyles.HexNumber,
                CultureInfo.InvariantCulture);
            var bytes = match.Groups["bytes"].Value;
            for (var offset = 0; offset + 2 < bytes.Length; offset += 3)
            {
                if (bytes[offset + 2] != ' ' || !byte.TryParse(bytes.AsSpan(offset, 2),
                        NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)) break;

                // Anche un fetch di prefisso deve restare sulla riga della stessa istruzione.
                lines.TryAdd(address++, lineNumber);
            }
        }
        return lines;
    }
}
