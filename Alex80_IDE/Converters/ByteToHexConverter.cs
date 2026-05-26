using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Alex80_IDE.Converters;

public class ByteToHexConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is byte b)
        {
            return b.ToString("X2");
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}