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
            OnPropertyChanged(nameof(CpuName));
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

    /// <summary>
    /// Spiega a chi si applicano le impostazioni mostrate nel pannello.
    /// <para>
    /// Senza un progetto aperto non sono solo dei valori predefiniti, come diceva prima
    /// questo testo: sono le impostazioni con cui "Assembla" lavora davvero sul sorgente
    /// selezionato (vedi <see cref="CreateSingleFileBuildRequest"/>), e insieme quelle da
    /// cui parte un progetto nuovo. Per questo il pannello resta attivo anche senza
    /// progetto, e non esiste un secondo posto dove modificare gli stessi valori.
    /// </para>
    /// </summary>
    public string SettingsScopeText =>
        CurrentProject is null
            ? "Valgono per l'assemblata del sorgente selezionato, e sono i valori da cui parte un progetto nuovo."
            : $"Valgono per il progetto «{CurrentProject.DisplayName}» e sono salvate nel suo file.";

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

    /// <summary>CPU accettate dall'assembler.</summary>
    public string[] AvailableCpus { get; } = { "Z80", "R800", "Z280", "EZ80" };

    /// <summary>
    /// CPU di destinazione del progetto aperto, o default se non ce n'e' uno.
    /// Una direttiva .CPU nel sorgente resta valida e ha la precedenza da quel
    /// punto in poi: questa impostazione decide solo da cosa si parte.
    /// </summary>
    public string CpuName
    {
        get => CurrentProject?.CpuName ?? UserSettings.Load().CpuName;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (CurrentProject is not null)
            {
                CurrentProject.CpuName = value;
                SaveProject();
            }
            else
            {
                UpdateSettings(settings => settings.CpuName = value);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(EffectiveCpuName));
            NotifyCpuContextChanged();
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
            CpuName = settings.CpuName,
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
                this,
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

        AddFilesToProject(paths);
    }

    /// <summary>
    /// Aggiunge dei sorgenti al progetto aperto. E' pubblico perche' lo usa anche il
    /// trascinamento di file sull'elenco, che non passa da una finestra di dialogo.
    /// </summary>
    public void AddFilesToProject(IEnumerable<string> paths)
    {
        if (CurrentProject is null)
        {
            return;
        }

        var added = false;

        foreach (var path in paths)
        {
            // Una cartella trascinata per sbaglio, o un percorso che nel frattempo e'
            // sparito, non deve finire nel progetto come voce inesistente.
            if (!File.Exists(path))
            {
                continue;
            }

            CurrentProject.AddFile(Path.GetFullPath(path));
            added = true;
        }

        if (!added)
        {
            return;
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
        AssemblerStatus = CreateAssemblerStatus(result) + CpuMismatchSuffix;
        (SaveArduinoArrayCommand as RelayCommand)?.RaiseCanExecuteChanged();

        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AddressHex = result.FirstAddress.ToString(UseWideAddresses ? "X6" : "X4");
            NumBytes = result.Bytes.Length.ToString();
            // Niente cast a ushort: con l'eZ80 l'indirizzo e' a 24 bit e
            // FFF000h diventerebbe F000h.
            CreateArray(result.Bytes, (uint)result.FirstAddress);
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

    /// <summary>
    /// Coda dell'esito quando il progetto assembla per una CPU che la scheda collegata
    /// non ha. L'assemblata si fa lo stesso, con la CPU scritta nel progetto: cambiarla
    /// di nascosto vorrebbe dire produrre un binario diverso da quello chiesto.
    /// </summary>
    private string CpuMismatchSuffix => HasCpuMismatch ? $" · progetto {CpuName}, assemblato per Z80 (scheda {CardName})" : string.Empty;

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
            CpuName = EffectiveCpuName,
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
            CpuName = EffectiveCpuName,
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

    /// <summary>
    /// Intestazione messa in cima al listato quando il progetto ha per bersaglio una CPU
    /// che la scheda collegata non monta.
    /// <para>
    /// E' scritta come commento perche' il listato finisce anche su disco come .lst:
    /// cosi' resta un listato valido e si porta dietro il motivo per cui e' stato
    /// prodotto per una CPU diversa da quella scritta nel progetto. Chi lo ritrova
    /// mesi dopo capisce perche' e' quello che e'.
    /// </para>
    /// </summary>
    private string CpuMismatchListingHeader
    {
        get
        {
            if (!HasCpuMismatch)
            {
                return string.Empty;
            }

            var rule = new string('=', 68);

            return string.Join(Environment.NewLine,
                $"; {rule}",
                $"; ATTENZIONE: il progetto è impostato sulla CPU {CpuName}, ma la scheda",
                $"; collegata è {CardName}, che monta uno Z80.",
                "; Questo listato è stato prodotto per Z80, non per la CPU del progetto.",
                "; Il progetto non è stato modificato: la sua impostazione torna a valere",
                "; appena la scheda viene scollegata.",
                $"; {rule}",
                string.Empty,
                string.Empty);
        }
    }

    private void ShowListing(string listingText)
    {
        // L'avviso entra anche nel file su disco, non solo nella scheda dell'editor.
        listingText = CpuMismatchListingHeader + listingText;

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
