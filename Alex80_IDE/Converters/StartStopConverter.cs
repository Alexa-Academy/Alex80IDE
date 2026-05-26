using System;
using System.Globalization;
using Avalonia.Data.Converters;

public class StartStopConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
            return isRunning ? "Stop" : "Start";

        return "Start";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}