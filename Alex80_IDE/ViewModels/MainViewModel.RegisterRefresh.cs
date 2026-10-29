using CommunityToolkit.Mvvm.ComponentModel;
using Alex80_IDE.Models;

public partial class MainViewModel
{
    [ObservableProperty] private bool _autoRefreshRegisters = true;
    [ObservableProperty] private bool _hasRegisterUpdateSupport;
    [ObservableProperty] private bool _hasRegisterSnapshotSupport;
    [ObservableProperty] private string _registerRefreshStatus = "Nessuna lettura dei registri ricevuta.";
    private bool _manualRegisterRefreshRequested;
    private bool _registersAreCurrent;
    private LogElement? _confirmedManualRegisterPulse;

    partial void OnAutoRefreshRegistersChanged(bool value)
    {
        UpdateSettings(settings => settings.AutoRefreshRegisters = value);
        OnPropertyChanged(nameof(RegisterRefreshToolTip));
        NotifyManualClockAvailability();
    }

    partial void OnHasRegisterUpdateSupportChanged(bool value)
    {
        OnPropertyChanged(nameof(RegisterRefreshToolTip));
        NotifyManualClockAvailability();
    }

    partial void OnHasRegisterSnapshotSupportChanged(bool value)
    {
        OnPropertyChanged(nameof(RegisterRefreshToolTip));
        NotifyManualClockAvailability();
        NotifyDumpAvailabilityChanged();
    }

    private bool CanSnapshotManualRegisters => IsAlex80Micro && HasRegisterSnapshotSupport &&
        IsMemoryFromArduino && IsClockFromArduino && !IsClockActive && !DebugEnabled &&
        _lastManualClockSample is { ClockNumber: { } clock } sample &&
        (sample.IsInstructionStartTState || clock == _completedManualInstructionClock);

    public string RegisterRefreshToolTip => HasRegisterSnapshotSupport
        ? "Rilegge i registri dopo gli step. Con clock manuale raggiunge il fetch successivo e usa temporaneamente 1 kHz per la lettura; i T-state del programma restano nella traccia."
        : HasRegisterUpdateSupport
        ? "Rilegge i registri dopo gli step in modalità Debug con WAIT. Il clock manuale Arduino non legge i registri e non aggiunge WAIT o clock del debugger."
        : "Applica i dump automatici dopo gli step in modalità Debug. Il clock manuale Arduino genera solo gli impulsi richiesti.";

    private void MarkRegistersStale()
    {
        _registersAreCurrent = false;
        RegisterRefreshStatus = _hasRegisterSnapshot
            ? "Ultima lettura conservata · registri da aggiornare."
            : "Nessuna lettura dei registri ricevuta.";
        OnPropertyChanged(nameof(InstructionPrediction));
        UpdateManualInstructionStatus();
    }

    private void UpdateManualInstructionStatus()
    {
        if (_instructionFromManualClock && CurrentInstruction is not null)
            InstructionStatus = _registersAreCurrent
                ? "Istruzione corrente · clock manuale fermo · registri aggiornati"
                : "Istruzione corrente · clock manuale fermo · registri non riletti";
    }

    private bool ShouldApplyRegisterDump()
    {
        bool apply = AutoRefreshRegisters || _manualRegisterRefreshRequested;
        _manualRegisterRefreshRequested = false;
        return apply;
    }

    private void RequestManualRegisterRefresh()
    {
        _manualRegisterRefreshRequested = CanSnapshotManualRegisters
            ? _serialManager.SnapshotManualRegisters() : _serialManager.RequestRegisters();
        if (_manualRegisterRefreshRequested) RegisterRefreshStatus = "Lettura dei registri in corso…";
    }
}
