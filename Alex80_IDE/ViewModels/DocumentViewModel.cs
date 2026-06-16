using System.Collections.ObjectModel;
using Alex80_IDE.Models;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class DocumentViewModel : ObservableObject
{
    private bool _isSyncingText;
    private bool _isInitializingText;

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

            if (!_isInitializingText)
            {
                IsDirty = true;
            }
        };
    }

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private bool _isDirty;
    
    [ObservableProperty]
    private string _text = string.Empty;

    public TextDocument EditorDocument { get; } = new();

    partial void OnTextChanged(string? value)
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
        _isInitializingText = true;
        EditorDocument.Text = text;
        _isInitializingText = false;
        _isSyncingText = false;
    }

    public void MarkSaved(string filePath)
    {
        FilePath = filePath;
        FileName = System.IO.Path.GetFileName(filePath);
        IsDirty = false;
    }

    public void ReloadFromDisk(string text)
    {
        _isInitializingText = true;
        Text = text;
        _isInitializingText = false;
        IsDirty = false;
    }
    
    public ObservableCollection<ListingLine> ListingLines { get; } = new();
    
}
