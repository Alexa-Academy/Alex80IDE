using Avalonia;
using Avalonia.Controls;

namespace Alex80_IDE.Controls;

/// <summary>
/// Console seriale in sola lettura, con scroll automatico sull'ultima riga ricevuta.
/// </summary>
public partial class SerialConsoleView : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SerialConsoleView, string>(nameof(Title), "Seriale");

    public SerialConsoleView()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            textBox.CaretIndex = textBox.Text?.Length ?? 0;
            textBox.BringIntoView();
        }
    }
}
