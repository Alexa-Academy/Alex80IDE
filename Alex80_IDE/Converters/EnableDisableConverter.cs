using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Alex80_IDE.Converters;

public class EnableDisableConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isEnable)
            return isEnable ? "Disabilita" : "Abilita";

        return "Start";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}