using System;
using System.Diagnostics;
using Alex80_IDE.InstructionReference;
using Alex80_IDE.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel
{
    private readonly ClockTraceTracker _clockTrace = new();
    private uint? _instructionPc;
    private bool _instructionNeedsRead;
    private bool _instructionFromManualClock, _manualFetchActive;
    private LogElement? _lastManualClockSample;
    [ObservableProperty] private bool _hasClockPulseSupport;
    partial void OnHasClockPulseSupportChanged(bool value) => NotifyManualClockAvailability();
    private bool IsManualInstructionCompleted => _instructionFromManualClock && CurrentInstruction is not null &&
        (_boundaryRegisterPosition is not null || _lastManualClockSample?.ClockNumber is { } clock &&
         clock == _completedManualInstructionClock);
    public string InstructionTitle => IsManualInstructionCompleted ? "ISTRUZIONE COMPLETATA"
        : _instructionFromManualClock ? "ISTRUZIONE SUL BUS" : "ISTRUZIONE AL PC";
    public bool CanPulseClock => IsSerialOpened && CanControlClock && HasClockPulseSupport &&
        !IsInstructionClockRunning && !IsClockActive && !DebugEnabled && !_serialManager.IsReadPending && !_serialManager.IsClockPulsePending && !_serialManager.IsRegisterRequestPending;
    public string ClockPulseToolTip => !HasClockPulseSupport ? "Richiede il manager Arduino aggiornato con supporto clk pulse."
        : !IsClockFromArduino ? "Clock esterno: Arduino non può generare impulsi. Usa Debug e Step con WAIT, se supportati dal firmware."
        : IsClockActive ? "Ferma il clock automatico per inviare un impulso."
        : DebugEnabled ? "Disattiva Debug per avanzare senza WAIT."
        : "Clock Arduino: invia solo un ciclo LOW–HIGH–LOW e lascia il clock fermo, senza WAIT o letture dei registri. Funziona anche con log disattivati.";
    public string StepToolTip => !IsEz80 && IsClockFromArduino && HasClockPulseSupport && !IsClockActive
        ? InstructionClockToolTip
        : IsEz80 ? "Esegue una sola istruzione via ZDI, entrando nelle CALL."
        : "Avanza di un'istruzione usando Debug con WAIT, entrando nelle CALL. Richiede il supporto del firmware e un clock in moto.";
    private void NotifyManualClockAvailability()
    {
        OnPropertyChanged(nameof(CanPulseClock)); OnPropertyChanged(nameof(ClockPulseToolTip));
        OnPropertyChanged(nameof(InstructionClockToolTip));
        OnPropertyChanged(nameof(StepToolTip));
        StepCommand.NotifyCanExecuteChanged();
        PulseClockCommand.NotifyCanExecuteChanged();
        AdvanceInstructionClockCommand.NotifyCanExecuteChanged();
    }
    [RelayCommand(CanExecute = nameof(CanPulseClock))]
    private void PulseClock()
    {
        if (!_serialManager.PulseClock()) InstructionStatus = "Impossibile inviare l'impulso di clock.";
        else MarkRegistersStale();
    }
    [ObservableProperty] private InstructionInfo? _currentInstruction;
    [ObservableProperty] private string _instructionStatus = "Ferma la CPU e rileggi i registri per vedere l'istruzione al PC.";
    [ObservableProperty] private string _clockSummary = "Nessun intervallo completo misurato.";
    public bool HasInstruction => CurrentInstruction is not null;
    public string InstructionIdealTime => IsClockFromArduino ? CurrentInstruction?.IdealTime(int.TryParse(ClockFrequency, out var hz) ? hz : 0) ?? "" : "";
    public string InstructionPrediction
    {
        get
        {
            if (_instructionFromManualClock || !_hasRegisterSnapshot || !_registersAreCurrent || CurrentInstruction is not { } info) return "";
            var parts = info.Mnemonic.Split(' ', 2);
            if (parts.Length != 2 || parts[0] is not ("INC" or "DEC")) return "";
            byte? value = parts[1] switch { "A" => AReg, "B" => BReg, "C" => CReg, "D" => DReg, "E" => EReg, "H" => HReg, "L" => LReg, _ => null };
            return value is { } before ? $"Risultato previsto: {parts[1]} = {before:X2}h → {unchecked((byte)(before + (parts[0] == "INC" ? 1 : -1))):X2}h" : "";
        }
    }
    partial void OnCurrentInstructionChanged(InstructionInfo? value)
    {
        OnPropertyChanged(nameof(InstructionTitle));
        OnPropertyChanged(nameof(HasInstruction)); OnPropertyChanged(nameof(InstructionIdealTime));
        OnPropertyChanged(nameof(InstructionPrediction));
    }
    private void InitializeInstructionView()
    {
        _serialManager.InstructionBytesReceived += (pc, bytes) => Dispatcher.UIThread.Post(() =>
        {
            if (_instructionPc != pc) return;
            CurrentInstruction = InstructionDecoder.Decode(pc, bytes, IsEz80, IsAdl);
            InstructionStatus = _instructionFromManualClock
                ? (_registersAreCurrent ? "Istruzione corrente · clock manuale fermo · registri aggiornati" : "Istruzione corrente · clock manuale fermo · registri non riletti")
                : "Prossima istruzione · byte letti dalla scheda";
            UpdateManualInstructionStatus();
            ResumeInstructionClockAfterDecode();
        });
        _serialManager.InstructionReadFailed += (pc, message) => Dispatcher.UIThread.Post(() =>
        {
            if (_instructionPc == pc)
            {
                CurrentInstruction = null; InstructionStatus = message;
                if (IsInstructionClockRunning) FinishInstructionClock(message);
            }
        });
        _serialManager.ReadStateChanged += () => Dispatcher.UIThread.Post(() =>
        {
            OnPropertyChanged(nameof(CanRun));
            OnPropertyChanged(nameof(CanUseDebug));
            DebugCommand.NotifyCanExecuteChanged();
            ReadMemoryCommand.NotifyCanExecuteChanged(); WriteMemoryCommand.NotifyCanExecuteChanged();
            StepCommand.NotifyCanExecuteChanged();
            StartStopBoardCommand.NotifyCanExecuteChanged();
            NotifyManualClockAvailability();
            NotifyDumpAvailabilityChanged();
            if (!_serialManager.IsReadPending && !_serialManager.IsClockPulsePending && !_serialManager.IsRegisterRequestPending) TryReadInstruction();
        });
        _serialManager.ClockPulseReceived += log => Dispatcher.UIThread.Post(() =>
        {
            ObserveManualClock(log);
            ObserveInstructionClockPulse(log);
        });
        _serialManager.ManualRegisterReadCompleted += log => Dispatcher.UIThread.Post(() =>
        {
            _clockTrace.ConfirmManualRegisterRead(log);
            _confirmedManualRegisterPulse = log;
        });
        _serialManager.BoundaryRegisterReadCompleted += (before, after, pc) => Dispatcher.UIThread.Post(() =>
        {
            _clockTrace.ConfirmBoundaryRegisterRead(before, after, pc);
            ClockSummary = _clockTrace.Summary;
            _boundaryRegisterPosition = (after, pc);
            OnPropertyChanged(nameof(InstructionTitle));
            _completedManualInstructionClock = after;
            UpdateManualInstructionStatus();
            _confirmedManualRegisterPulse = null;
            if (_instructionClockBeforeBoundarySnapshot is { } status)
                InstructionClockStatus = status + " · registri aggiornati.";
            _instructionClockBeforeBoundarySnapshot = null;
        });
        _serialManager.ClockPulseFailed += error => Dispatcher.UIThread.Post(() =>
        {
            _manualRegisterRefreshRequested = false;
            _boundaryRegisterPosition = null;
            if (_instructionClockBeforeBoundarySnapshot is { } status)
                InstructionClockStatus = status + " · " + error;
            _instructionClockBeforeBoundarySnapshot = null;
            MarkRegistersStale();
            RegisterRefreshStatus = error;
            _clockTrace.InvalidateSynchronization(); InvalidateInstruction(); InstructionStatus = error;
            if (IsInstructionClockRunning) FinishInstructionClock(error);
        });
    }
    private void RefreshStoppedInstruction(uint pc, bool force = false, bool manualClock = false)
    {
        _instructionFromManualClock = manualClock;
        OnPropertyChanged(nameof(InstructionTitle)); OnPropertyChanged(nameof(InstructionPrediction));
        if (_instructionPc == pc && ((!force && CurrentInstruction is not null) || _serialManager.IsReadPending))
        { OnPropertyChanged(nameof(InstructionPrediction)); return; }
        _instructionPc = pc; _instructionNeedsRead = true;
        CurrentInstruction = null; InstructionStatus = $"PC {pc:X4} · lettura dell'istruzione…";
        TryReadInstruction();
    }
    private void TryReadInstruction()
    {
        if ((IsInstructionClockRunning && _instructionClockStepper?.NeedsInstruction != true) ||
            !_instructionNeedsRead || _instructionPc is not { } pc || _serialManager.IsReadPending ||
            _serialManager.IsClockPulsePending || _serialManager.IsRegisterRequestPending) return;
        bool read = IsInstructionClockRunning && _instructionClockReader is { } reader
            ? reader(pc) : _serialManager.ReadInstruction(pc, IsEz80, IsAdl);
        if (read) _instructionNeedsRead = false;
        else
        {
            InstructionStatus = $"PC {pc:X4} · byte dell'istruzione non disponibili.";
            if (IsInstructionClockRunning) FinishInstructionClock(InstructionStatus);
        }
    }
    private void InvalidateInstruction()
    {
        _instructionPc = null; _instructionNeedsRead = false; CurrentInstruction = null;
        _instructionFromManualClock = _manualFetchActive = false;
        _lastManualClockSample = null;
        _completedManualInstructionClock = null;
        _confirmedManualRegisterPulse = null;
        _boundaryRegisterPosition = null;
        OnPropertyChanged(nameof(InstructionTitle)); OnPropertyChanged(nameof(InstructionPrediction));
        InstructionStatus = "Ferma la CPU e rileggi i registri per vedere l'istruzione al PC.";
    }
    private void ObserveInstructionLog(LogElement log)
    {
        _clockTrace.Observe(log); ClockSummary = _clockTrace.Summary;
        if (log.IsReset) { InvalidateInstruction(); MarkRegistersStale(); }
        else if (log.HardwareHalt == true) { InvalidateInstruction(); MarkRegistersStale(); InstructionStatus = "CPU in HALT: eseguire reset per riprendere."; }
        else if (!log.IsInjected && log.Waiting == true && DebugEnabled) RefreshStoppedInstruction(log.Address);
    }
    private void ObserveManualClock(LogElement log)
    {
        if (!IsBoardConnected || IsEz80 || IsClockActive || DebugEnabled || log.IsReset || log.HardwareHalt == true) return;
        _lastManualClockSample = log;
        _confirmedManualRegisterPulse = null;
        _boundaryRegisterPosition = null;
        bool firstFetch = log.StartsInstruction == true && !log.Z80_M1 && log.Z80_IORQ && log.Z80_RFSH && !log.IsInjected;
        if (firstFetch && !_manualFetchActive) RefreshStoppedInstruction(log.Address, force: true, manualClock: true);
        _manualFetchActive = firstFetch;
        if (_instructionPc is null) InstructionStatus = "Clock inviato · avanza fino al prossimo fetch per identificare l'istruzione.";
        NotifyDumpAvailabilityChanged();
    }
    private bool CanStepCpu() => IsBoardConnected && !IsInstructionClockRunning &&
        !_serialManager.IsReadPending && !_serialManager.IsClockPulsePending && !_serialManager.IsRegisterRequestPending &&
        (IsEz80 || !IsClockFromArduino || !HasClockPulseSupport || IsClockActive || !DebugEnabled);
    private bool CanToggleBoardClock() => CanControlClock && !IsInstructionClockRunning && !_serialManager.IsReadPending && !_serialManager.IsClockPulsePending && !_serialManager.IsRegisterRequestPending;
    [RelayCommand]
    private void OpenInstructionManual()
    {
        if (CurrentInstruction is { } info) Process.Start(new ProcessStartInfo(info.ManualUrl) { UseShellExecute = true });
    }
}
