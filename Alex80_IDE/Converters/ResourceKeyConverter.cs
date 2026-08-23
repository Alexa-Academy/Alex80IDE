using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace Alex80_IDE.Converters;

/// <summary>
/// Risolve una chiave di risorsa dell'applicazione nel valore corrispondente: serve dove
/// la risorsa da usare e' scelta dal view model, come le icone delle tool window, e non
/// puo' quindi essere indicata con StaticResource nel markup.
/// </summary>
public sealed class ResourceKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string key || Application.Current is not { } application)
        {
            return null;
        }

        return application.TryGetResource(key, application.ActualThemeVariant, out var resource)
            ? resource
            : null;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
