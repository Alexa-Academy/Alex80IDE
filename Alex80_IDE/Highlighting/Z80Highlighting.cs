using System;
using System.Xml;
using Avalonia.Platform;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace Alex80_IDE.Highlighting;

public static class Z80Highlighting
{
    private const string Name = "Z80";
    private static bool _registered;

    public static void Register()
    {
        if (_registered || HighlightingManager.Instance.GetDefinition(Name) is not null)
        {
            _registered = true;
            return;
        }

        using var stream = AssetLoader.Open(new Uri("avares://Alex80_IDE/Assets/Highlighting/Z80.xshd"));
        using var reader = XmlReader.Create(stream);
        var xshd = HighlightingLoader.LoadXshd(reader);
        var definition = HighlightingLoader.Load(xshd, HighlightingManager.Instance);

        HighlightingManager.Instance.RegisterHighlighting(Name, new[] { ".asm", ".z80", ".s", ".inc" }, definition);
        _registered = true;
    }
}
