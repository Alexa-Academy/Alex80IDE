using System;
using Alex80_IDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel
{
    private InstructionClockStepper? _instructionClockStepper;
    private Func<bool>? _instructionClockPulseSender;
    private Func<uint, bool>? _instructionClockReader;
    private uint? _completedManualInstructionClock;
    private bool _instructionClockStopRequested;
    private Func<uint, bool>? _boundaryRegisterSnapshotSender;
    private string? _instructionClockBeforeBoundarySnapshot;
    private uint? _instructionClockRegisterPc;
    private byte? _instructionClockFlags, _instructionClockB;
    [ObservableProperty] private bool _isInstructionClockRunning;
    [ObservableProperty] private string _instructionClockStatus = string.Empty;

    public string InstructionClockToolTip => !IsClockFromArduino
        ? "Clock esterno: usa Debug e Step con WAIT, se supportati dal firmware. Gli impulsi manuali richiedono il clock da Arduino."
        : "Completa i T-state rimanenti e si ferma prima del T1 successivo. " +
        "Richiede clock automatico fermo e Debug disattivato. " +
        (HasRegisterBoundarySnapshotSupport && AutoRefreshRegisters
            ? "Poi legge i registri con clock debugger e ripristina PC e SP prima del prossimo T1. "
            : "Senza letture automatiche dei registri. ") +
        "Per durate condizionali servono registri aggiornati.";

    partial void OnIsInstructionClockRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanUseDebug));
        OnPropertyChanged(nameof(CanUseBusLog));
        DebugCommand.NotifyCanExecuteChanged();
        EnableDisableLogsCommand.NotifyCanExecuteChanged();
        StepCommand.NotifyCanExecuteChanged();
        StartStopBoardCommand.NotifyCanExecuteChanged();
        NotifyManualClockAvailability();
        NotifyDumpAvailabilityChanged();
        ReadMemoryCommand.NotifyCanExecuteChanged();
        WriteMemoryCommand.NotifyCanExecuteChanged();
        StopInstructionClockCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanPulseClock))]
    private void AdvanceInstructionClock() => StartInstructionClock(() => _serialManager.PulseClock(), null);

    private void StartInstructionClock(Func<bool> sendPulse, Func<uint, bool>? readInstruction,
        Func<uint, bool>? readRegisters = null)
    {
        _instructionClockStepper = new InstructionClockStepper(_boundaryRegisterPosition?.Clock ?? _lastManualClockSample?.ClockNumber,
            _confirmedManualRegisterPulse);
        _boundaryRegisterSnapshotSender = readRegisters;
        _instructionClockRegisterPc = _boundaryRegisterPosition is not null && _registersAreCurrent ? PcReg : null;
        _instructionClockFlags = _instructionClockRegisterPc is not null ? FReg : null;
        _instructionClockB = _instructionClockRegisterPc is not null ? BReg : null;
        _instructionClockPulseSender = sendPulse;
        _instructionClockReader = readInstruction;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = true;
        InstructionClockStatus = "Completamento dell'istruzione senza WAIT…";
        try
        {
            if (_confirmedManualRegisterPulse is not null &&
                (CurrentInstruction is null || _clockTrace.CurrentTState != 2 ||
                 _clockTrace.CurrentInstructionAddress != CurrentInstruction.Address))
                throw new InvalidOperationException("Posizione T-state dopo la lettura dei registri sconosciuta: leggere l'istruzione o usare i clock singoli fino a T1.");
            if (_lastManualClockSample?.Waiting == true && _confirmedManualRegisterPulse != _lastManualClockSample)
                throw new InvalidOperationException("La traccia proviene da un WAIT: riprendere con un clock singolo prima dell'avanzamento contato.");
            if (_boundaryRegisterPosition is null && CurrentInstruction is { } instruction && _lastManualClockSample?.ClockNumber is { } clock &&
                clock != _completedManualInstructionClock && _clockTrace.CurrentTState is { } elapsed &&
                _clockTrace.CurrentInstructionAddress == instruction.Address)
                _instructionClockStepper.SetInstruction(instruction, elapsed, clock,
                    _registersAreCurrent && PcReg == instruction.Address ? FReg : null,
                    _registersAreCurrent && PcReg == instruction.Address ? BReg : null);
        }
        catch (InvalidOperationException ex) { FinishInstructionClock(ex.Message); return; }
        MarkRegistersStale();
        SendNextInstructionClockPulse();
    }

    private void SendNextInstructionClockPulse()
    {
        try
        {
            if (!IsSerialOpened || !CanControlClock || IsClockActive || DebugEnabled)
            {
                FinishInstructionClock("Configurazione del clock cambiata: avanzamento interrotto.");
                return;
            }
            // Only program clocks: no automatic dump or debugger WAIT.
            if (_instructionClockPulseSender?.Invoke() != true)
                FinishInstructionClock("Impossibile inviare l'impulso di clock: avanzamento interrotto.");
        }
        catch (Exception ex)
        {
            _serialManager.CancelReadForReset();
            FinishInstructionClock($"Avanzamento interrotto: {ex.Message}");
        }
    }

    private void ObserveInstructionClockPulse(LogElement sample)
    {
        if (_instructionClockStepper is not { } stepper) return;
        try
        {
            bool complete = stepper.Observe(sample);
            string count = stepper.Pulses == 1 ? "1 colpo di clock" : $"{stepper.Pulses} colpi di clock";
            if (_instructionClockStopRequested)
                FinishInstructionClock($"Avanzamento interrotto · {count} · clock fermo.");
            else if (complete)
            {
                _completedManualInstructionClock = sample.ClockNumber;
                FinishInstructionClock($"Istruzione completata · {count} · clock fermo prima del prossimo T1.", readRegisters: true);
            }
            else if (stepper.NeedsInstruction)
            {
                InstructionClockStatus = "Clock fermo su T1 · lettura dell'istruzione…";
                // Opcode reads do not pulse CLK; await the complete response
                // before planning the remaining T-states or sending a pulse.
                RefreshStoppedInstruction(sample.Address, force: true, manualClock: true);
            }
            else
            {
                InstructionClockStatus = $"Avanzamento in corso · {count}…";
                SendNextInstructionClockPulse();
            }
        }
        catch (InvalidOperationException ex) { FinishInstructionClock(ex.Message); }
    }

    private void ResumeInstructionClockAfterDecode()
    {
        if (_instructionClockStepper is not { NeedsInstruction: true } stepper ||
            CurrentInstruction is not { } instruction || _lastManualClockSample?.ClockNumber is not { } clock) return;
        try
        {
            stepper.SetInstruction(instruction, 1, clock,
                _instructionClockRegisterPc == instruction.Address ? _instructionClockFlags : null,
                _instructionClockRegisterPc == instruction.Address ? _instructionClockB : null);
            SendNextInstructionClockPulse();
        }
        catch (InvalidOperationException ex) { FinishInstructionClock(ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(IsInstructionClockRunning))]
    private void StopInstructionClock()
    {
        if (!_serialManager.IsClockPulsePending)
        {
            FinishInstructionClock("Avanzamento interrotto · clock fermo.");
            return;
        }
        // Await the pulse already sent; never truncate it or send another one.
        _instructionClockStopRequested = true;
        InstructionClockStatus = "Arresto al termine dell'impulso in corso…";
    }

    private void FinishInstructionClock(string status, bool readRegisters = false)
    {
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockReader = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = status;
        UpdateManualInstructionStatus();
        if (readRegisters && AutoRefreshRegisters && CanSnapshotBoundaryRegisters)
        {
            _instructionClockBeforeBoundarySnapshot = status;
            if (RequestBoundaryRegisterRefresh(_boundaryRegisterSnapshotSender))
                InstructionClockStatus += " · lettura dei registri…";
            else
            {
                _instructionClockBeforeBoundarySnapshot = null;
                InstructionClockStatus += " · lettura dei registri non avviata.";
            }
        }
        _boundaryRegisterSnapshotSender = null;
        TryReadInstruction();
    }

    private void CancelInstructionClock()
    {
        _instructionClockBeforeBoundarySnapshot = null;
        _boundaryRegisterSnapshotSender = null;
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockReader = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = string.Empty;
    }
}
