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
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Empty;
        //throw new NotSupportedException();
    }
}