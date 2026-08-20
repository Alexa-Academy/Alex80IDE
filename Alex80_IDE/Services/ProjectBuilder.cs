using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Alex80_IDE.Models;
using Konamiman.Nestor80.Assembler;

namespace Alex80_IDE.Services;

/// <summary>Un sorgente da assemblare, con il testo già preso dall'editor se il file è aperto.</summary>
public sealed record BuildSource(string DisplayName, string? FilePath, string Text);

/// <summary>Un errore o un avviso prodotto dall'assemblatore, con il file da cui arriva.</summary>
public sealed record BuildMessage(string FileName, int? LineNumber, bool IsWarning, string Text)
{
    public override string ToString() =>
        $"{FileName}{(LineNumber is null ? "" : ":" + LineNumber)} {(IsWarning ? "Warning" : "Error")} - {Text}";
}

public sealed class BuildRequest
{
    public required IReadOnlyList<BuildSource> Sources { get; init; }
    public ProjectLinkMode LinkMode { get; init; } = ProjectLinkMode.SeparateUnits;
    public byte FillByte { get; init; }
    public bool TasmCompatibility { get; init; } = true;

    /// <summary>Cartelle in cui cercare i file richiesti da INCLUDE/INCBIN.</summary>
    public IReadOnlyList<string> IncludeDirectories { get; init; } = Array.Empty<string>();
}

public sealed class BuildResult
{
    public byte[] Bytes { get; init; } = Array.Empty<byte>();
    public int FirstAddress { get; init; }
    public string Listing { get; init; } = string.Empty;
    public IReadOnlyList<BuildMessage> Messages { get; init; } = Array.Empty<BuildMessage>();

    /// <summary>Per ogni file assemblato: nome, indirizzo iniziale e dimensione del blocco prodotto.</summary>
    public IReadOnlyList<(string FileName, int Address, int Size)> Chunks { get; init; } =
        Array.Empty<(string, int, int)>();

    public bool Success => Bytes.Length > 0 && Messages.All(m => m.IsWarning);
    public int ErrorCount => Messages.Count(m => !m.IsWarning);
}

/// <summary>
/// Assembla i file di un progetto (o un singolo sorgente) e li mette insieme in un'unica
/// immagine di memoria.
/// </summary>
public static class ProjectBuilder
{
    private const int MemorySize = 65536;

    public static BuildResult Build(BuildRequest request)
    {
        var messages = new List<BuildMessage>();

        if (request.Sources.Count == 0)
        {
            messages.Add(new BuildMessage("progetto", null, false, "Il progetto non contiene nessun file da assemblare."));
            return new BuildResult { Messages = messages };
        }

        return request.LinkMode is ProjectLinkMode.SingleUnit && request.Sources.Count > 1
            ? BuildSingleUnit(request, messages)
            : BuildSeparateUnits(request, messages);
    }

    /// <summary>
    /// Ogni file viene assemblato per conto suo e piazzato in memoria al proprio ORG,
    /// oppure subito dopo il file precedente se non ne ha uno.
    /// </summary>
    private static BuildResult BuildSeparateUnits(BuildRequest request, List<BuildMessage> messages)
    {
        var memory = CreateMemory(request.FillByte);
        var chunks = new List<(string, int, int)>();
        var listing = new StringBuilder();
        int min = int.MaxValue, max = 0;
        ushort nextAddress = 0;

        foreach (var source in request.Sources)
        {
            var config = CreateConfiguration(request, source);
            config.StartAddress = nextAddress;

            var result = Assemble(source, config, messages, listing, request.Sources.Count > 1);
            if (result is null)
            {
                continue;
            }

            using var outputStream = new MemoryStream();
            var size = OutputGenerator.GenerateAbsolute(result, outputStream, fillByte: request.FillByte);
            if (size <= 0)
            {
                chunks.Add((source.DisplayName, result.FirstAddress, 0));
                continue;
            }

            var bytes = outputStream.ToArray();
            var address = result.FirstAddress;

            if (address + bytes.Length > MemorySize)
            {
                messages.Add(new BuildMessage(source.DisplayName, null, false,
                    $"Il blocco non ci sta in memoria: {bytes.Length} byte a partire da {address:X4}h."));
                continue;
            }

            var overlapping = chunks.FirstOrDefault(c =>
                c.Item3 > 0 && address < c.Item2 + c.Item3 && c.Item2 < address + bytes.Length);
            if (overlapping.Item3 > 0)
            {
                messages.Add(new BuildMessage(source.DisplayName, null, false,
                    $"Il blocco ({address:X4}h-{address + bytes.Length - 1:X4}h) si sovrappone a quello di " +
                    $"{overlapping.Item1} ({overlapping.Item2:X4}h-{overlapping.Item2 + overlapping.Item3 - 1:X4}h)."));
                continue;
            }

            Array.Copy(bytes, 0, memory, address, bytes.Length);
            chunks.Add((source.DisplayName, address, bytes.Length));
            min = Math.Min(min, address);
            max = Math.Max(max, address + bytes.Length);
            nextAddress = (ushort)(address + bytes.Length);
        }

        return CreateResult(memory, min, max, chunks, listing.ToString(), messages);
    }

    /// <summary>
    /// I file vengono assemblati insieme come un unico sorgente: si genera al volo un
    /// sorgente radice che li include tutti nell'ordine dell'elenco, così i simboli
    /// definiti in un file sono visibili in tutti gli altri.
    /// </summary>
    private static BuildResult BuildSingleUnit(BuildRequest request, List<BuildMessage> messages)
    {
        var root = new StringBuilder();
        foreach (var source in request.Sources)
        {
            root.AppendLine($"\tINCLUDE \"{source.DisplayName.Replace("\"", "\"\"")}\"");
        }
        root.AppendLine("\tEND");

        var rootSource = new BuildSource("progetto", null, root.ToString());
        var config = CreateConfiguration(request, rootSource);

        // L'END in fondo a ognuno dei file inclusi deve chiudere solo quel file,
        // non fermare tutta l'assemblata.
        config.IgnoreEndInstructionInIncludedFiles = true;

        var listing = new StringBuilder();
        var result = Assemble(rootSource, config, messages, listing, includeHeader: false);
        if (result is null)
        {
            return new BuildResult { Messages = messages };
        }

        var memory = CreateMemory(request.FillByte);
        using var outputStream = new MemoryStream();
        var size = OutputGenerator.GenerateAbsolute(result, outputStream, fillByte: request.FillByte);
        if (size <= 0)
        {
            return new BuildResult { Messages = messages, Listing = listing.ToString() };
        }

        var bytes = outputStream.ToArray();
        Array.Copy(bytes, 0, memory, result.FirstAddress, bytes.Length);

        var chunks = new List<(string, int, int)> { ("progetto", result.FirstAddress, bytes.Length) };
        return CreateResult(memory, result.FirstAddress, result.FirstAddress + bytes.Length, chunks,
            listing.ToString(), messages);
    }

    private static AssemblyResult? Assemble(
        BuildSource source,
        AssemblyConfiguration config,
        List<BuildMessage> messages,
        StringBuilder listing,
        bool includeHeader)
    {
        AssemblyResult result;

        try
        {
            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(source.Text));
            result = AssemblySourceProcessor.Assemble(sourceStream, Encoding.UTF8, config);
        }
        catch (Exception ex)
        {
            messages.Add(new BuildMessage(source.DisplayName, null, false, $"({ex.GetType().Name}) {ex.Message}"));
            return null;
        }

        foreach (var error in result.Errors)
        {
            messages.Add(new BuildMessage(
                error.IncludeFileName ?? source.DisplayName,
                error.LineNumber,
                error.IsWarning,
                error.Message));
        }

        if (result.HasErrors)
        {
            return null;
        }

        AppendListing(result, source, listing, includeHeader);
        return result;
    }

    private static void AppendListing(AssemblyResult result, BuildSource source, StringBuilder listing, bool includeHeader)
    {
        try
        {
            using var listingStream = new MemoryStream();
            using (var writer = new StreamWriter(listingStream, Encoding.UTF8, leaveOpen: true))
            {
                ListingFileGenerator.GenerateListingFile(result, writer, new ListingFileConfiguration());
                writer.Flush();
            }

            listingStream.Position = 0;
            using var reader = new StreamReader(listingStream);

            if (includeHeader)
            {
                if (listing.Length > 0)
                {
                    listing.AppendLine();
                }

                listing.AppendLine($";{new string('=', 78)}");
                listing.AppendLine($"; {source.DisplayName}");
                listing.AppendLine($";{new string('=', 78)}");
            }

            listing.Append(reader.ReadToEnd());
        }
        catch (Exception ex)
        {
            listing.AppendLine($"; impossibile generare il listato di {source.DisplayName}: {ex.Message}");
        }
    }

    private static AssemblyConfiguration CreateConfiguration(BuildRequest request, BuildSource source)
    {
        var searchDirectories = BuildSearchDirectories(request, source);

        Stream? OpenInclude(string fileName) => OpenIncludedFile(fileName, request, searchDirectories);

        return new AssemblyConfiguration
        {
            CpuName = "Z80",
            BuildType = BuildType.Absolute,
            MaxErrors = 0,
            TasmCompatibility = request.TasmCompatibility,
            GetStreamForInclude = OpenInclude,
            GetStreamForIncbin = OpenInclude
        };
    }

    /// <summary>
    /// Risolve un file richiesto da INCLUDE/INCBIN: prima fra i file del progetto (così si usa
    /// il testo dell'editor e le modifiche non salvate entrano nell'assemblata), poi su disco.
    /// </summary>
    private static Stream? OpenIncludedFile(string fileName, BuildRequest request, IReadOnlyList<string> searchDirectories)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        var known = request.Sources.FirstOrDefault(s =>
            string.Equals(s.DisplayName, fileName, StringComparison.OrdinalIgnoreCase) ||
            (s.FilePath is not null && string.Equals(s.FilePath, fileName, StringComparison.OrdinalIgnoreCase)));

        if (known is not null)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(known.Text));
        }

        if (Path.IsPathRooted(fileName))
        {
            return File.Exists(fileName) ? File.OpenRead(fileName) : null;
        }

        foreach (var directory in searchDirectories)
        {
            var candidate = Path.GetFullPath(Path.Combine(directory, fileName));
            if (File.Exists(candidate))
            {
                return File.OpenRead(candidate);
            }
        }

        return null;
    }

    private static IReadOnlyList<string> BuildSearchDirectories(BuildRequest request, BuildSource source)
    {
        var directories = new List<string>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) &&
                !directories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(directory);
            }
        }

        Add(source.FilePath);

        foreach (var directory in request.IncludeDirectories)
        {
            if (!directories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(directory);
            }
        }

        foreach (var other in request.Sources)
        {
            Add(other.FilePath);
        }

        directories.Add(Directory.GetCurrentDirectory());
        return directories;
    }

    private static byte[] CreateMemory(byte fillByte)
    {
        var memory = new byte[MemorySize];
        if (fillByte != 0)
        {
            Array.Fill(memory, fillByte);
        }

        return memory;
    }

    private static BuildResult CreateResult(
        byte[] memory,
        int min,
        int max,
        IReadOnlyList<(string, int, int)> chunks,
        string listing,
        IReadOnlyList<BuildMessage> messages)
    {
        if (min == int.MaxValue || max <= min || messages.Any(m => !m.IsWarning))
        {
            return new BuildResult { Messages = messages, Listing = listing, Chunks = chunks };
        }

        return new BuildResult
        {
            Bytes = memory[min..max],
            FirstAddress = min,
            Chunks = chunks,
            Listing = listing,
            Messages = messages
        };
    }
}
