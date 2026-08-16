using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;

namespace Alex80_IDE.Converters;

/// <summary>
/// Sigla del tipo di file mostrata nella linguetta dell'editor: "ASM", "LST", "BIN"...
/// </summary>
public class FileTypeBadgeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var extension = Path.GetExtension(value as string ?? string.Empty).TrimStart('.');

        return extension.Length == 0 ? "TXT" : extension.ToUpperInvariant();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
