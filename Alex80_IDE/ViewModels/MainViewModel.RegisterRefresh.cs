using CommunityToolkit.Mvvm.ComponentModel;
using Alex80_IDE.Models;

public partial class MainViewModel
{
    [ObservableProperty] private bool _autoRefreshRegisters = true;
    [ObservableProperty] private bool _hasRegisterUpdateSupport;
    [ObservableProperty] private bool _hasRegisterSnapshotSupport;
    [ObservableProperty] private bool _hasRegisterBoundarySnapshotSupport;
    [ObservableProperty] private string _registerRefreshStatus = "Nessuna lettura dei registri ricevuta.";
    private bool _manualRegisterRefreshRequested;
    private bool _registersAreCurrent;
    private LogElement? _confirmedManualRegisterPulse;
    private (uint Clock, ushort Pc)? _boundaryRegisterPosition;

    partial void OnHasRegisterBoundarySnapshotSupportChanged(bool value)
    {
        OnPropertyChanged(nameof(RegisterRefreshToolTip));
        NotifyManualClockAvailability();
        NotifyDumpAvailabilityChanged();
    }

    private bool CanSnapshotBoundaryRegisters => IsAlex80Micro && HasRegisterBoundarySnapshotSupport &&
        IsMemoryFromArduino && IsClockFromArduino && !IsClockActive && !DebugEnabled &&
        (_boundaryRegisterPosition is not null || _lastManualClockSample?.ClockNumber is { } clock &&
         clock == _completedManualInstructionClock);

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
        (sample.IsInstructionStartTState || clock == _completedManualInstructionClock || _boundaryRegisterPosition is not null);

    public string RegisterRefreshToolTip => HasRegisterBoundarySnapshotSupport
        ? "Aggiorna i registri dopo una sola istruzione e ripristina PC e SP prima del prossimo T1. I clock della lettura sono conteggiati come debugger."
        : HasRegisterSnapshotSupport
        ? "Rilegge i registri dopo gli step in modalità Debug con WAIT. Con clock manuale usa Rileggi dalla scheda: la richiesta esplicita raggiunge T1/T2 del fetch successivo e legge i registri a 1 kHz."
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
            InstructionStatus = _boundaryRegisterPosition is not null
                ? (_registersAreCurrent ? "Prossima istruzione · clock fermo prima di T1 · registri aggiornati"
                    : "Prossima istruzione · registri da aggiornare")
                : _registersAreCurrent
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
        if (CanSnapshotBoundaryRegisters) { RequestBoundaryRegisterRefresh(); return; }
        _manualRegisterRefreshRequested = CanSnapshotManualRegisters
            ? _serialManager.SnapshotManualRegisters() : _serialManager.RequestRegisters();
        if (_manualRegisterRefreshRequested) RegisterRefreshStatus = "Lettura dei registri in corso…";
    }

    private bool RequestBoundaryRegisterRefresh(System.Func<uint, bool>? sender = null)
    {
        uint? clock = _boundaryRegisterPosition?.Clock ?? _lastManualClockSample?.ClockNumber;
        _manualRegisterRefreshRequested = clock is { } before &&
            (sender ?? _serialManager.SnapshotBoundaryRegisters)(before);
        if (_manualRegisterRefreshRequested) RegisterRefreshStatus = "Lettura dei registri prima del prossimo T1…";
        return _manualRegisterRefreshRequested;
    }
}
