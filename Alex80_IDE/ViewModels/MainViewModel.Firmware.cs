using System;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel
{
    // Raise this when the IDE requires a newer ALEX80u_manager_terminal release.
    // Other board families retain their existing protocol and capabilities.
    public const string RequiredMicroFirmwareVersion = "1.0.0";

    [ObservableProperty]
    private string _firmwareVersion = string.Empty;

    public string RequiredFirmwareVersion => IsAlex80Micro ? RequiredMicroFirmwareVersion : string.Empty;

    private Version? ParsedFirmwareVersion => Version.TryParse(FirmwareVersion, out var version)
        ? new Version(version.Major, version.Minor, Math.Max(0, version.Build), Math.Max(0, version.Revision))
        : null;

    public bool IsFirmwareUpdateRequired => IsBoardConnected && IsAlex80Micro &&
        ParsedFirmwareVersion is { } version && version < new Version(RequiredMicroFirmwareVersion);

    public bool IsFirmwareVersionUnknown => IsBoardConnected && IsAlex80Micro && ParsedFirmwareVersion is null;

    public bool HasFirmwareWarning => IsFirmwareUpdateRequired || IsFirmwareVersionUnknown;

    public string CardConnectionText
    {
        get
        {
            if (!IsBoardConnected) return CardBadgeText;
            var text = CardName;
            if (!string.IsNullOrWhiteSpace(FirmwareVersion)) text += $" · firmware {FirmwareVersion}";
            if (IsFirmwareUpdateRequired) text += " · aggiornamento richiesto";
            else if (IsFirmwareVersionUnknown) text += " · versione da verificare";
            return text;
        }
    }

    public string CardConnectionToolTip
    {
        get
        {
            if (!IsBoardConnected) return "Nessuna scheda connessa.";
            if (!IsAlex80Micro) return CardConnectionText;
            var requirement = $"Versione minima del firmware ALEX80µ richiesta dall'IDE: {RequiredFirmwareVersion}.";
            if (IsFirmwareVersionUnknown)
                return $"{requirement}\nLa scheda non comunica una versione valida: verificare il firmware e aggiornarlo se necessario.";
            return $"{requirement}\nVersione installata: {FirmwareVersion}. " +
                (IsFirmwareUpdateRequired ? "Aggiornare ALEX80u_manager_terminal." : "Il requisito di versione è soddisfatto.");
        }
    }

    partial void OnFirmwareVersionChanged(string value) => NotifyFirmwareStatusChanged();

    private void NotifyFirmwareStatusChanged()
    {
        OnPropertyChanged(nameof(RequiredFirmwareVersion));
        OnPropertyChanged(nameof(IsFirmwareUpdateRequired));
        OnPropertyChanged(nameof(IsFirmwareVersionUnknown));
        OnPropertyChanged(nameof(HasFirmwareWarning));
        OnPropertyChanged(nameof(CardConnectionText));
        OnPropertyChanged(nameof(CardConnectionToolTip));
    }
}
