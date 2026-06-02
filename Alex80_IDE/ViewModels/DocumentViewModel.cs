using System.Collections.ObjectModel;
using Alex80_IDE.Models;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class DocumentViewModel : ObservableObject
{
    private bool _isSyncingText;

    public DocumentViewModel()
    {
        EditorDocument.TextChanged += (_, _) =>
        {
            if (_isSyncingText)
            {
                return;
            }

            _isSyncingText = true;
            Text = EditorDocument.Text;
            _isSyncingText = false;
        };
    }

    [ObservableProperty]
    private string _fileName;
    
    [ObservableProperty]
    private string _text;

    public TextDocument EditorDocument { get; } = new();

    partial void OnTextChanged(string value)
    {
        if (_isSyncingText)
        {
            return;
        }

        var text = value ?? string.Empty;
        if (EditorDocument.Text == text)
        {
            return;
        }

        _isSyncingText = true;
        EditorDocument.Text = text;
        _isSyncingText = false;
    }
    
    public ObservableCollection<ListingLine> ListingLines { get; } = new();
    
}
