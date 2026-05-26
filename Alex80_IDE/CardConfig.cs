using System;

namespace Alex80_IDE;

public class CardConfig
{
    public string CardName { get; set; }   // ALEX80 o ALEX80µ
    public bool IsClockFromArduino { get; set; }   // true se 'A' o 'E'
    public int ClockFrequency { get; set; }    // es. 50
    public bool ClockActive { get; set; }   // true se 1, false se 0
    public bool IsMemFromArduino { get; set; }   // 'A' o 'E'
    
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

