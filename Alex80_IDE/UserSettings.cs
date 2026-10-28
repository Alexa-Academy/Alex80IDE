using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Alex80_IDE;

public sealed class UserSettings
{
    public string Theme { get; set; } = AppTheme.Dark.ToString();
    public double? MainWindowWidth { get; set; }
    public double? MainWindowHeight { get; set; }
    public bool MainWindowMaximized { get; set; }

    /// <summary>Ultima velocità selezionata per la connessione seriale.</summary>
    public int BaudRate { get; set; } = 115200;

    /// <summary>Rilegge i registri dopo gli step e ai confini delle istruzioni nel clock manuale.</summary>
    public bool AutoRefreshRegisters { get; set; } = true;

    /// <summary>Accetta anche la sintassi TASM (.EQU, etichette senza due punti, $ esadecimale...).</summary>
    public bool TasmCompatibility { get; set; } = true;

    /// <summary>CPU usata quando non c'e' un progetto aperto.</summary>
    public string CpuName { get; set; } = "Z80";

    /// <summary>Byte con cui riempire le posizioni di memoria che nessun file occupa.</summary>
    public byte FillByte { get; set; }

    /// <summary>Ultimo progetto aperto, viene riaperto all'avvio.</summary>
    public string? LastProjectPath { get; set; }

    /// <summary>Stato dei dock delle tool window, per identificativo del dock.</summary>
    public Dictionary<string, ToolDockSettings> Docks { get; set; } = new();

    public static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Alex80_IDE",
            "settings.json");

    public static UserSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public void Save()
    {
        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }
}

/// <summary>Stato salvato di un dock: pannello aperto o chiuso, strumento mostrato, misura.</summary>
public sealed class ToolDockSettings
{
    public bool IsOpen { get; set; } = true;

    public string? SelectedTool { get; set; }

    /// <summary>Misura del pannello per ogni strumento del dock, per identificativo.</summary>
    public Dictionary<string, double> ToolSizes { get; set; } = new();
}
