using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace Alex80_IDE.Converters;

/// <summary>Stato del clock per la status bar.</summary>
public class ClockStateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "clock in esecuzione" : "clock fermo";
        }

        return "clock fermo";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
