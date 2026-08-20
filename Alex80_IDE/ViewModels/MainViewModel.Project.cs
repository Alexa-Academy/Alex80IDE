using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Alex80_IDE;
using Alex80_IDE.Models;
using Alex80_IDE.Services;
using Alex80_IDE.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;

/// <summary>
/// Gestione del progetto: elenco dei file, impostazioni di assemblaggio e costruzione
/// dell'immagine di memoria a partire da tutti i file che ne fanno parte.
/// </summary>
public partial class MainViewModel
{
    private Alex80Project? _currentProject;

    /// <summary>I file del progetto, nell'ordine in cui vengono assemblati.</summary>
    public ObservableCollection<ProjectFileViewModel> ProjectFiles { get; } = new();

    public ICommand NewProjectCommand { get; private set; } = null!;
    public ICommand OpenProjectCommand { get; private set; } = null!;
    public ICommand SaveProjectCommand { get; private set; } = null!;
    public ICommand CloseProjectCommand { get; private set; } = null!;
    public ICommand AddFilesToProjectCommand { get; private set; } = null!;

    public Alex80Project? CurrentProject
    {
        get => _currentProject;
        private set
        {
            _currentProject = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasProject));
            OnPropertyChanged(nameof(ProjectTitle));
            OnPropertyChanged(nameof(SettingsScopeText));
            OnPropertyChanged(nameof(TasmCompatibility));
            OnPropertyChanged(nameof(FillByteText));
            OnPropertyChanged(nameof(IsSingleUnitMode));
            (AssembleCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CloseProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddFilesToProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool HasProject => CurrentProject is not null;

    public string ProjectTitle => CurrentProject?.DisplayName ?? "nessun progetto";

    /// <summary>Spiega a chi si applicano le impostazioni mostrate nel pannello.</summary>
    public string SettingsScopeText =>
        CurrentProject is null ? "valori predefiniti (nessun progetto aperto)" : "impostazioni del progetto";

    private void InitializeProjectCommands()
    {
        NewProjectCommand = new RelayCommand(async _ => await NewProjectAsync());
        OpenProjectCommand = new RelayCommand(async _ => await OpenProjectAsync());
        SaveProjectCommand = new RelayCommand(_ => SaveProject(), _ => CurrentProject is not null);
        CloseProjectCommand = new RelayCommand(_ => CloseProject(), _ => CurrentProject is not null);
        AddFilesToProjectCommand = new RelayCommand(async _ => await AddFilesToProjectAsync(), _ => CurrentProject is not null);

        RestoreLastProject();
    }

    // ---------------------------------------------------------------- impostazioni

    /// <summary>
    /// Se true l'assemblatore accetta anche la sintassi TASM (.EQU, .BYTE, etichette senza
    /// due punti, numeri esadecimali con prefisso $, operatori &amp; | ^ ~ &lt;&lt; &gt;&gt;)
    /// oltre a quella Macro80. Vale per il progetto aperto, o come default se non ce n'è uno.
    /// </summary>
    public bool TasmCompatibility
    {
        get => CurrentProject?.TasmCompatibility ?? UserSettings.Load().TasmCompatibility;
        set
        {
            if (CurrentProject is not null)
            {
                CurrentProject.TasmCompatibility = value;
                SaveProject();
            }
            else
            {
                UpdateSettings(settings => settings.TasmCompatibility = value);
            }

            OnPropertyChanged();
        }
    }

    /// <summary>Byte con cui si riempiono le posizioni di memoria che nessun file occupa.</summary>
    public string FillByteText
    {
        get => (CurrentProject?.FillByte ?? UserSettings.Load().FillByte).ToString("X2");
        set
        {
            if (!byte.TryParse(value?.Trim().TrimStart('$').Replace("0x", string.Empty), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out var fillByte))
            {
                OnPropertyChanged();
                return;
            }

            if (CurrentProject is not null)
            {
                CurrentProject.FillByte = fillByte;
                SaveProject();
            }
            else
            {
                UpdateSettings(settings => settings.FillByte = fillByte);
            }

            OnPropertyChanged();
        }
    }

    /// <summary>
    /// False = ogni file è un blocco a sé (nessun conflitto fra label omonime),
    /// true = i file sono un unico sorgente e si vedono i simboli a vicenda.
    /// </summary>
    public bool IsSingleUnitMode
    {
        get => CurrentProject?.LinkMode is ProjectLinkMode.SingleUnit;
        set
        {
            if (CurrentProject is null)
            {
                OnPropertyChanged();
                return;
            }

            CurrentProject.LinkMode = value ? ProjectLinkMode.SingleUnit : ProjectLinkMode.SeparateUnits;
            SaveProject();
            OnPropertyChanged();
        }
    }

    private static void UpdateSettings(Action<UserSettings> change)
    {
        try
        {
            var settings = UserSettings.Load();
            change(settings);
            settings.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Impossibile salvare le impostazioni: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- apertura e salvataggio

    private static Window? MainWindow =>
        App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;

    private async Task NewProjectAsync()
    {
        var dialog = new SaveFileDialog
        {
            InitialFileName = "progetto" + Alex80Project.FileExtension,
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Progetto Alex80", Extensions = { Alex80Project.FileExtension.TrimStart('.') } }
            }
        };

        var path = await dialog.ShowAsync(MainWindow);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!path.EndsWith(Alex80Project.FileExtension, StringComparison.OrdinalIgnoreCase))
        {
            path += Alex80Project.FileExtension;
        }

        var settings = UserSettings.Load();
        var project = new Alex80Project
        {
            Name = Path.GetFileNameWithoutExtension(path),
            TasmCompatibility = settings.TasmCompatibility,
            FillByte = settings.FillByte
        };

        // I sorgenti già aperti sono quasi sempre quelli che si vogliono nel progetto nuovo.
        project.ProjectPath = path;
        foreach (var document in OpenDocuments.Where(d => IsZ80Source(d) && !string.IsNullOrWhiteSpace(d.FilePath)))
        {
            project.AddFile(document.FilePath!);
        }

        try
        {
            project.Save(path);
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Progetto", $"Impossibile creare il progetto: {ex.Message}");
            return;
        }

        ApplyProject(project);
    }

    private async Task OpenProjectAsync()
    {
        var dialog = new OpenFileDialog
        {
            AllowMultiple = false,
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Progetto Alex80", Extensions = { Alex80Project.FileExtension.TrimStart('.') } }
            }
        };

        var paths = await dialog.ShowAsync(MainWindow);
        var path = paths?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            ApplyProject(Alex80Project.Load(path));
        }
        catch (Exception ex)
        {
            await _dialogService.ShowErrorAsync("Progetto", $"Impossibile aprire il progetto: {ex.Message}");
        }
    }

    private void SaveProject()
    {
        if (CurrentProject?.ProjectPath is null)
        {
            return;
        }

        try
        {
            CurrentProject.Save();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Impossibile salvare il progetto: {ex.Message}");
        }
    }

    private void CloseProject()
    {
        CurrentProject = null;
        ProjectFiles.Clear();
        UpdateSettings(settings => settings.LastProjectPath = null);
        AssemblerStatus = "nessuna assemblata";
    }

    private void RestoreLastProject()
    {
        try
        {
            var lastProjectPath = UserSettings.Load().LastProjectPath;
            if (!string.IsNullOrWhiteSpace(lastProjectPath) && File.Exists(lastProjectPath))
            {
                ApplyProject(Alex80Project.Load(lastProjectPath));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Impossibile riaprire l'ultimo progetto: {ex.Message}");
        }
    }

    private void ApplyProject(Alex80Project project)
    {
        CurrentProject = project;
        RefreshProjectFiles();
        UpdateSettings(settings => settings.LastProjectPath = project.ProjectPath);
    }

    private void RefreshProjectFiles()
    {
        ProjectFiles.Clear();

        if (CurrentProject is null)
        {
            return;
        }

        foreach (var relativePath in CurrentProject.Files)
        {
            ProjectFiles.Add(new ProjectFileViewModel(
                relativePath,
                CurrentProject.ToAbsolutePath(relativePath),
                string.Equals(relativePath, CurrentProject.MainFile, StringComparison.OrdinalIgnoreCase)));
        }
    }

    // ---------------------------------------------------------------- elenco dei file

    private async Task AddFilesToProjectAsync()
    {
        if (CurrentProject is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            AllowMultiple = true,
            Filters = new List<FileDialogFilter>
            {
                new() { Name = "Sorgenti Z80", Extensions = { "asm", "z80", "inc", "s", "txt" } },
                new() { Name = "Tutti i file", Extensions = { "*" } }
            }
        };

        var paths = await dialog.ShowAsync(MainWindow);
        if (paths is null || paths.Length == 0)
        {
            return;
        }

        foreach (var path in paths)
        {
            CurrentProject.AddFile(Path.GetFullPath(path));
        }

        SaveProject();
        RefreshProjectFiles();
    }

    [RelayCommand]
    private void RemoveProjectFile(ProjectFileViewModel? file)
    {
        if (CurrentProject is null || file is null)
        {
            return;
        }

        CurrentProject.RemoveFile(file.RelativePath);
        SaveProject();
        RefreshProjectFiles();
    }

    [RelayCommand]
    private void SetMainProjectFile(ProjectFileViewModel? file)
    {
        if (CurrentProject is null || file is null)
        {
            return;
        }

        CurrentProject.SetMainFile(file.RelativePath);
        SaveProject();
        RefreshProjectFiles();
    }

    [RelayCommand]
    private void MoveProjectFileUp(ProjectFileViewModel? file) => MoveProjectFile(file, -1);

    [RelayCommand]
    private void MoveProjectFileDown(ProjectFileViewModel? file) => MoveProjectFile(file, +1);

    private void MoveProjectFile(ProjectFileViewModel? file, int offset)
    {
        if (CurrentProject is null || file is null)
        {
            return;
        }

        CurrentProject.MoveFile(file.RelativePath, offset);
        SaveProject();
        RefreshProjectFiles();
    }

    [RelayCommand]
    private async Task OpenProjectFile(ProjectFileViewModel? file)
    {
        if (file is null)
        {
            return;
        }

        var existing = FindOpenDocument(file.AbsolutePath);
        if (existing is not null)
        {
            SelectedDocument = existing;
            return;
        }

        if (!File.Exists(file.AbsolutePath))
        {
            await _dialogService.ShowErrorAsync("Progetto", $"Il file {file.FileName} non esiste più.");
            file.IsMissing = true;
            return;
        }

        var document = new DocumentViewModel
        {
            FileName = file.FileName,
            FilePath = file.AbsolutePath,
            Text = await File.ReadAllTextAsync(file.AbsolutePath)
        };
        document.IsDirty = false;

        OpenDocuments.Add(document);
        SelectedDocument = document;
        StartWatchingDocument(document);
    }

    // ---------------------------------------------------------------- assemblaggio

    /// <summary>
    /// Assembla il progetto aperto (tutti i file, uniti in un'unica immagine di memoria)
    /// oppure, se non c'è nessun progetto, il solo sorgente selezionato.
    /// </summary>
    private void RunAssembler()
    {
        var request = CurrentProject is not null ? CreateProjectBuildRequest() : CreateSingleFileBuildRequest();
        if (request is null)
        {
            return;
        }

        BuildResult result;
        try
        {
            result = ProjectBuilder.Build(request);
        }
        catch (Exception ex)
        {
            AssemblerStatus = "assemblata fallita";
            ShowTextTab("Errore", $"Errore: {ex.Message}");
            return;
        }

        UpdateProjectFilePlacements(result);

        if (!result.Success)
        {
            var errorCount = result.ErrorCount;
            AssemblerStatus = errorCount == 1 ? "1 errore" : $"{errorCount} errori";
            ShowTextTab("Errori assembler", string.Join(Environment.NewLine, result.Messages));
            return;
        }

        _fileBytesToWrite = result.Bytes;
        AssemblerStatus = CreateAssemblerStatus(result);
        (SaveArduinoArrayCommand as RelayCommand)?.RaiseCanExecuteChanged();

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddressHex = result.FirstAddress.ToString("X4");
            NumBytes = result.Bytes.Length.ToString();
            CreateArray(result.Bytes, (ushort)result.FirstAddress);
        });

        ShowListing(result.Listing);
    }

    private string CreateAssemblerStatus(BuildResult result)
    {
        var status = $"assemblato · {result.Bytes.Length} byte da {result.FirstAddress:X4}";

        if (CurrentProject is null)
        {
            return status;
        }

        var fileCount = result.Chunks.Count(c => c.Size > 0);
        var mode = CurrentProject.LinkMode is ProjectLinkMode.SingleUnit ? "unità unica" : "unità separate";
        return CurrentProject.LinkMode is ProjectLinkMode.SingleUnit
            ? $"{status} · {ProjectFiles.Count} file, {mode}"
            : $"{status} · {fileCount} file, {mode}";
    }

    private BuildRequest? CreateProjectBuildRequest()
    {
        var project = CurrentProject!;
        var sources = new List<BuildSource>();
        var missing = new List<string>();

        foreach (var relativePath in project.Files)
        {
            var absolutePath = project.ToAbsolutePath(relativePath);
            var text = ReadSourceText(absolutePath);

            if (text is null)
            {
                missing.Add(Path.GetFileName(absolutePath));
                continue;
            }

            sources.Add(new BuildSource(Path.GetFileName(absolutePath), absolutePath, text));
        }

        if (missing.Count > 0)
        {
            AssemblerStatus = missing.Count == 1 ? "1 file mancante" : $"{missing.Count} file mancanti";
            ShowTextTab("Errori assembler",
                "File del progetto che non esistono più:" + Environment.NewLine +
                string.Join(Environment.NewLine, missing.Select(m => "  " + m)));
            RefreshProjectFiles();
            return null;
        }

        var includeDirectories = new List<string>();
        if (project.ProjectDirectory is not null)
        {
            includeDirectories.Add(project.ProjectDirectory);
        }

        return new BuildRequest
        {
            Sources = sources,
            LinkMode = project.LinkMode,
            FillByte = project.FillByte,
            TasmCompatibility = project.TasmCompatibility,
            IncludeDirectories = includeDirectories
        };
    }

    private BuildRequest? CreateSingleFileBuildRequest()
    {
        if (!IsZ80Source(SelectedDocument))
        {
            return null;
        }

        var document = SelectedDocument;
        var settings = UserSettings.Load();

        return new BuildRequest
        {
            Sources = new[] { new BuildSource(document.FileName, document.FilePath, document.Text) },
            LinkMode = ProjectLinkMode.SeparateUnits,
            FillByte = settings.FillByte,
            TasmCompatibility = settings.TasmCompatibility,
            IncludeDirectories = OpenDocuments
                .Select(d => d.FilePath)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => Path.GetDirectoryName(p!)!)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    /// <summary>Il testo del file, preso dall'editor se è aperto (così valgono anche le modifiche non salvate).</summary>
    private string? ReadSourceText(string absolutePath)
    {
        var openDocument = FindOpenDocument(absolutePath);
        if (openDocument is not null)
        {
            return openDocument.Text;
        }

        try
        {
            return File.Exists(absolutePath) ? File.ReadAllText(absolutePath) : null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void UpdateProjectFilePlacements(BuildResult result)
    {
        foreach (var file in ProjectFiles)
        {
            file.IsMissing = !File.Exists(file.AbsolutePath);

            var chunk = result.Chunks.FirstOrDefault(c =>
                string.Equals(c.FileName, file.FileName, StringComparison.OrdinalIgnoreCase));

            file.Placement = chunk.Size > 0 ? $"{chunk.Address:X4}-{chunk.Address + chunk.Size - 1:X4}" : string.Empty;
        }
    }

    // ---------------------------------------------------------------- schede di output

    private void ShowTextTab(string fileName, string text)
    {
        var tab = new DocumentViewModel { FileName = fileName, Text = text };
        OpenDocuments.Add(tab);
        SelectedDocument = tab;
    }

    private void ShowListing(string listingText)
    {
        var listingFileName = CurrentProject is not null
            ? CurrentProject.DisplayName + ".lst"
            : Path.ChangeExtension(SelectedDocument!.FileName, ".lst")!;

        var listingPath = CurrentProject?.ProjectDirectory is not null
            ? Path.Combine(CurrentProject.ProjectDirectory, listingFileName)
            : string.IsNullOrWhiteSpace(SelectedDocument?.FilePath)
                ? null
                : Path.ChangeExtension(SelectedDocument.FilePath, ".lst");

        var listingTab = GetOrCreateListingDocument(listingPath ?? listingFileName, listingFileName);

        if (listingPath is not null)
        {
            try
            {
                File.WriteAllText(listingPath, listingText);
                listingTab.MarkSaved(listingPath);
                StartWatchingDocument(listingTab);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Impossibile scrivere il listato: {ex.Message}");
            }
        }

        listingTab.ReloadFromDisk(listingText);
        SelectedDocument = listingTab;
    }

    private DocumentViewModel GetOrCreateListingDocument(string key, string fileName)
    {
        if (_listingDocuments.TryGetValue(key, out var listingDocument) && OpenDocuments.Contains(listingDocument))
        {
            return listingDocument;
        }

        listingDocument = FindOpenDocument(key) ?? new DocumentViewModel { FileName = fileName };

        if (!OpenDocuments.Contains(listingDocument))
        {
            OpenDocuments.Add(listingDocument);
        }

        _listingDocuments[key] = listingDocument;
        return listingDocument;
    }
}
