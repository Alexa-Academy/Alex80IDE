using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Alex80_IDE.ViewModels;

/// <summary>
/// Una riga dell'elenco dei file del progetto.
/// <para>
/// Le azioni sulla riga sono comandi della riga stessa, non del view model principale:
/// il menu contestuale vive in un popup fuori dall'albero visuale della finestra, dove
/// un binding a un antenato non arriva, mentre il DataContext della riga ci arriva.
/// </para>
/// </summary>
public partial class ProjectFileViewModel : ObservableObject
{
    private readonly MainViewModel _owner;

    public ProjectFileViewModel(MainViewModel owner, string relativePath, string absolutePath, bool isMain)
    {
        _owner = owner;
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

    [RelayCommand]
    private void Open() => _owner.OpenProjectFileCommand.Execute(this);

    [RelayCommand]
    private void MakeMain() => _owner.SetMainProjectFileCommand.Execute(this);

    [RelayCommand]
    private void MoveUp() => _owner.MoveProjectFileUpCommand.Execute(this);

    [RelayCommand]
    private void MoveDown() => _owner.MoveProjectFileDownCommand.Execute(this);

    [RelayCommand]
    private void Remove() => _owner.RemoveProjectFileCommand.Execute(this);

    /// <summary>Aggiunge altri sorgenti al progetto: e' l'unica azione che non riguarda questa riga.</summary>
    [RelayCommand]
    private void AddFiles() => _owner.AddFilesToProjectCommand.Execute(null);
}
