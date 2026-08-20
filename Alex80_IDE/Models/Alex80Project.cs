using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Alex80_IDE.Models;

/// <summary>
/// Come vengono messi insieme i file elencati nel progetto.
/// </summary>
public enum ProjectLinkMode
{
    /// <summary>
    /// Ogni file è assemblato per conto suo e poi piazzato in memoria al proprio ORG,
    /// oppure in coda al file precedente se non ne ha uno. I file non si vedono i
    /// simboli a vicenda, quindi non ci sono conflitti fra label con lo stesso nome.
    /// </summary>
    SeparateUnits,

    /// <summary>
    /// I file sono assemblati insieme come se fossero un unico sorgente, nell'ordine
    /// dell'elenco: una label definita in un file è visibile in tutti gli altri.
    /// </summary>
    SingleUnit
}

/// <summary>
/// Un progetto Alex80: l'elenco ordinato dei file che compongono un programma,
/// più le impostazioni con cui assemblarli. Viene salvato in un file .a80proj
/// e i percorsi dei sorgenti sono relativi alla cartella che lo contiene.
/// </summary>
public sealed class Alex80Project
{
    public const string FileExtension = ".a80proj";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Nome mostrato nel pannello; se vuoto si usa il nome del file di progetto.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Percorsi dei sorgenti, relativi alla cartella del progetto, nell'ordine di assemblaggio.</summary>
    public List<string> Files { get; set; } = new();

    /// <summary>Il file principale, cioè il primo a essere assemblato. È sempre il primo di <see cref="Files"/>.</summary>
    public string? MainFile { get; set; }

    public ProjectLinkMode LinkMode { get; set; } = ProjectLinkMode.SeparateUnits;

    /// <summary>Valore usato per le posizioni di memoria che nessun file riempie.</summary>
    public byte FillByte { get; set; }

    /// <summary>Accetta anche la sintassi TASM oltre a quella Macro80.</summary>
    public bool TasmCompatibility { get; set; } = true;

    /// <summary>Percorso del file .a80proj. Non viene serializzato.</summary>
    [JsonIgnore]
    public string? ProjectPath { get; set; }

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name
        : ProjectPath is not null ? Path.GetFileNameWithoutExtension(ProjectPath)
        : "progetto senza nome";

    [JsonIgnore]
    public string? ProjectDirectory =>
        ProjectPath is null ? null : Path.GetDirectoryName(Path.GetFullPath(ProjectPath));

    /// <summary>I percorsi assoluti dei sorgenti, nell'ordine di assemblaggio.</summary>
    public IEnumerable<string> AbsoluteFilePaths => Files.Select(ToAbsolutePath);

    public string ToAbsolutePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || ProjectDirectory is null)
        {
            return relativePath;
        }

        return Path.GetFullPath(Path.Combine(ProjectDirectory, relativePath));
    }

    public string ToProjectPath(string absolutePath)
    {
        if (ProjectDirectory is null)
        {
            return absolutePath;
        }

        try
        {
            var relative = Path.GetRelativePath(ProjectDirectory, absolutePath);
            // Se il file sta su un altro volume, GetRelativePath restituisce il percorso assoluto.
            return relative;
        }
        catch (ArgumentException)
        {
            return absolutePath;
        }
    }

    /// <summary>
    /// Aggiunge un sorgente al progetto, se non c'è già. Restituisce true se è stato aggiunto.
    /// </summary>
    public bool AddFile(string absolutePath)
    {
        var relativePath = ToProjectPath(absolutePath);

        if (Files.Any(f => string.Equals(ToAbsolutePath(f), absolutePath, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        Files.Add(relativePath);
        MainFile ??= relativePath;
        return true;
    }

    public void RemoveFile(string relativePath)
    {
        Files.Remove(relativePath);

        if (string.Equals(MainFile, relativePath, StringComparison.OrdinalIgnoreCase))
        {
            MainFile = Files.FirstOrDefault();
        }
    }

    /// <summary>
    /// Marca un file come principale portandolo in cima all'elenco: l'ordine dell'elenco
    /// è anche l'ordine di assemblaggio, quindi il principale è sempre il primo.
    /// </summary>
    public void SetMainFile(string relativePath)
    {
        if (!Files.Remove(relativePath))
        {
            return;
        }

        Files.Insert(0, relativePath);
        MainFile = relativePath;
    }

    public void MoveFile(string relativePath, int offset)
    {
        var index = Files.IndexOf(relativePath);
        var newIndex = index + offset;

        if (index < 0 || newIndex < 0 || newIndex >= Files.Count)
        {
            return;
        }

        Files.RemoveAt(index);
        Files.Insert(newIndex, relativePath);
        MainFile = Files.FirstOrDefault();
    }

    public static Alex80Project Load(string projectPath)
    {
        var json = File.ReadAllText(projectPath);
        var project = JsonSerializer.Deserialize<Alex80Project>(json, JsonOptions)
                      ?? throw new InvalidDataException("Il file di progetto è vuoto o non valido.");

        project.ProjectPath = Path.GetFullPath(projectPath);
        project.Files ??= new List<string>();

        // Il principale è per definizione il primo dell'elenco: se il file salvato dice
        // altrimenti (o indica un file non più presente) riallineiamo le due cose.
        if (project.MainFile is not null && project.Files.Contains(project.MainFile))
        {
            project.SetMainFile(project.MainFile);
        }
        else
        {
            project.MainFile = project.Files.FirstOrDefault();
        }

        return project;
    }

    public void Save(string? projectPath = null)
    {
        //I percorsi sono relativi alla cartella del progetto: se stiamo salvando altrove
        //vanno ricalcolati, altrimenti punterebbero a file inesistenti.
        var absolutePaths = AbsoluteFilePaths.ToList();
        var absoluteMainFile = MainFile is null ? null : ToAbsolutePath(MainFile);

        ProjectPath = Path.GetFullPath(projectPath ?? ProjectPath
            ?? throw new InvalidOperationException("Il progetto non ha un percorso su cui essere salvato."));

        Files = absolutePaths.Select(ToProjectPath).ToList();
        MainFile = absoluteMainFile is null ? Files.FirstOrDefault() : ToProjectPath(absoluteMainFile);

        var directory = Path.GetDirectoryName(ProjectPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(ProjectPath, JsonSerializer.Serialize(this, JsonOptions));
    }
}
