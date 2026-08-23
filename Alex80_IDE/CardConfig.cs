using System;

namespace Alex80_IDE;

public class CardConfig
{
    public string CardName { get; set; }   // ALEX80 o ALEX80µ
    public bool IsClockFromArduino { get; set; }   // true se 'A' o 'E'
    public int ClockFrequency { get; set; }    // es. 50
    public bool ClockActive { get; set; }   // true se 1, false se 0
    public bool IsMemFromArduino { get; set; }   // 'A' o 'E'

    /// <summary>
    /// Nome con cui la scheda di test eZ80 si presenta nel messaggio !INFO.
    /// </summary>
    public const string Ez80CardName = "eZ80Test";

    /// <summary>
    /// Vero quando la scheda collegata e' l'eZ80 via ZDI e non uno Z80 Alex80.
    /// Abilita indirizzi a 24 bit e il dump registri esteso (!DUMP24).
    /// </summary>
    /// <summary>Vero per l'ALEX80 modulare, non per la versione micro.</summary>
    public bool IsAlex80Full =>
        CardName is not null &&
        CardName.StartsWith("ALEX80", StringComparison.OrdinalIgnoreCase) &&
        !IsAlex80Micro;

    /// <summary>Vero per l'ALEX80 micro (shield per Arduino Uno).</summary>
    public bool IsAlex80Micro =>
        CardName is not null &&
        (CardName.Contains('\u00B5') || CardName.Contains("micro", StringComparison.OrdinalIgnoreCase));

    public bool IsEz80 => string.Equals(CardName, Ez80CardName, StringComparison.OrdinalIgnoreCase);

    
    public static CardConfig Parse(string input)
    {
        var result = new CardConfig();
        var parts = input.Split(',');

        foreach (var part in parts)
        {
            var kv = part.Split(':');
            if (kv.Length != 2)
                continue;

            var key = kv[0].Trim();
            var value = kv[1].Trim();

            switch (key)
            {
                case "CARD":
                    result.CardName = value;
                    break;
                case "SCLK":
                    result.IsClockFromArduino = value[0] == 'A';
                    break;
                case "FCLK":
                    result.ClockFrequency = int.Parse(value);
                    break;
                case "ECLK":
                    result.ClockActive = value == "1";
                    break;
                case "SMEM":
                    result.IsMemFromArduino = value[0] == 'A';
                    break;
            }
        }

        return result;
    }
}

