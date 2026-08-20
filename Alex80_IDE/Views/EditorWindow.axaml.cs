using Alex80_IDE.Helpers;
using Avalonia.Controls;

namespace Alex80_IDE;

public partial class EditorWindow : Window
{
    public EditorWindow()
    {
        InitializeComponent();
        AppShortcuts.Install(this);
    }
}
