using System;
using System.IO;
using System.Text.Json;

namespace Alex80_IDE;

public sealed class UserSettings
{
    public string Theme { get; set; } = AppTheme.Dark.ToString();
    public double? MainWindowWidth { get; set; }
    public double? MainWindowHeight { get; set; }
    public bool MainWindowMaximized { get; set; }

    /// <summary>Accetta anche la sintassi TASM (.EQU, etichette senza due punti, $ esadecimale...).</summary>
    public bool TasmCompatibility { get; set; } = true;

    /// <summary>Byte con cui riempire le posizioni di memoria che nessun file occupa.</summary>
    public byte FillByte { get; set; }

    /// <summary>Ultimo progetto aperto, viene riaperto all'avvio.</summary>
    public string? LastProjectPath { get; set; }

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
