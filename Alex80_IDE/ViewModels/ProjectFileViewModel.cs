using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Alex80_IDE.ViewModels;

/// <summary>Una riga dell'elenco dei file del progetto.</summary>
public partial class ProjectFileViewModel : ObservableObject
{
    public ProjectFileViewModel(string relativePath, string absolutePath, bool isMain)
    {
        RelativePath = relativePath;
        AbsolutePath = absolutePath;
        _isMain = isMain;
        _isMissing = !File.Exists(absolutePath);
    }

    /// <summary>Il percorso come è scritto nel file di progetto: è la chiave di ogni operazione.</summary>
    public string RelativePath { get; }

    public string AbsolutePath { get; }

    public string FileName => Path.GetFileName(AbsolutePath);

    /// <summary>La sottocartella, mostrata sotto al nome solo quando il file non sta accanto al progetto.</summary>
    public string SubFolder
    {
        get
        {
            var folder = Path.GetDirectoryName(RelativePath);
            return string.IsNullOrEmpty(folder) || folder == "." ? string.Empty : folder;
        }
    }

    public bool HasSubFolder => SubFolder.Length > 0;

    [ObservableProperty]
    private bool _isMain;

    [ObservableProperty]
    private bool _isMissing;

    /// <summary>Indirizzo e dimensione assegnati dall'ultima assemblata, per l'elenco nel pannello.</summary>
    [ObservableProperty]
    private string _placement = string.Empty;
}
