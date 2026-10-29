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
    [ObservableProperty] private bool _isInstructionClockRunning;
    [ObservableProperty] private string _instructionClockStatus = string.Empty;

    public string InstructionClockToolTip => !IsClockFromArduino
        ? "Clock esterno: usa Debug e Step con WAIT, se supportati dal firmware. Gli impulsi manuali richiedono il clock da Arduino."
        : "Completa i T-state rimanenti e si ferma prima del T1 successivo. " +
        "Richiede clock automatico fermo e Debug disattivato. " +
        "Senza WAIT o letture dei registri. Per durate condizionali servono registri aggiornati.";

    partial void OnIsInstructionClockRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanUseDebug));
        OnPropertyChanged(nameof(CanUseBusLog));
        OnPropertyChanged(nameof(CanConfigureLogMode));
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

    private void StartInstructionClock(Func<bool> sendPulse, Func<uint, bool>? readInstruction)
    {
        _instructionClockStepper = new InstructionClockStepper(_lastManualClockSample?.ClockNumber);
        _instructionClockPulseSender = sendPulse;
        _instructionClockReader = readInstruction;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = true;
        InstructionClockStatus = "Completamento dell'istruzione senza WAIT…";
        try
        {
            if (_lastManualClockSample?.Waiting == true)
                throw new InvalidOperationException("La traccia proviene da un WAIT: riprendere con un clock singolo prima dell'avanzamento contato.");
            if (CurrentInstruction is { } instruction && _lastManualClockSample?.ClockNumber is { } clock &&
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
                FinishInstructionClock($"Istruzione completata · {count} · clock fermo prima del prossimo T1.");
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
            stepper.SetInstruction(instruction, 1, clock);
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

    private void FinishInstructionClock(string status)
    {
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockReader = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = status;
        TryReadInstruction();
    }

    private void CancelInstructionClock()
    {
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockReader = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = string.Empty;
    }
}
