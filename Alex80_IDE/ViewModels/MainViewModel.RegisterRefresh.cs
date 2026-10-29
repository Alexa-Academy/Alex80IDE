using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel
{
    [ObservableProperty] private bool _autoRefreshRegisters = true;
    [ObservableProperty] private bool _hasRegisterUpdateSupport;
    [ObservableProperty] private string _registerRefreshStatus = "Nessuna lettura dei registri ricevuta.";
    private bool _manualRegisterRefreshRequested;
    private bool _registersAreCurrent;

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

    public string RegisterRefreshToolTip => HasRegisterUpdateSupport
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
        _manualRegisterRefreshRequested = _serialManager.RequestRegisters();
        if (_manualRegisterRefreshRequested) RegisterRefreshStatus = "Lettura dei registri in corso…";
    }
}
