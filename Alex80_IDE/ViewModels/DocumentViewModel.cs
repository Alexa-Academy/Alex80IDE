using System.Collections.ObjectModel;
using Alex80_IDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class DocumentViewModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName;
    
    [ObservableProperty]
    private string _text;
    
    public ObservableCollection<ListingLine> ListingLines { get; } = new();
    
}
