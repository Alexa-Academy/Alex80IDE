using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class HexadecimalConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ushort ushortValue)
        {
            return $"{ushortValue:X4}"; // Formatta correttamente
        }
        if (value is uint uintValue)
        {
            // Indirizzi eZ80: 6 cifre appena escono dai 16 bit.
            return uintValue > 0xFFFF ? $"{uintValue:X6}" : $"{uintValue:X4}";
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Empty;
        //throw new NotSupportedException();
    }
}