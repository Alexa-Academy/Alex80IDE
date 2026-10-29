using System;
using Alex80_IDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel
{
    private InstructionClockStepper? _instructionClockStepper;
    private Func<bool>? _instructionClockPulseSender;
    private bool _instructionClockStopRequested;
    [ObservableProperty] private bool _isInstructionClockRunning;
    [ObservableProperty] private string _instructionClockStatus = string.Empty;

    public string InstructionClockToolTip =>
        "Completa l'istruzione in corso con impulsi singoli e si ferma sul T1 della successiva. " +
        "Richiede clock automatico fermo e Debug disattivato. " +
        "Non inietta letture dei registri: l'ultima lettura resta da aggiornare.";

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
    private void AdvanceInstructionClock() => StartInstructionClock(() => _serialManager.PulseClock());

    private void StartInstructionClock(Func<bool> sendPulse)
    {
        _instructionClockStepper = new InstructionClockStepper();
        _instructionClockPulseSender = sendPulse;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = true;
        MarkRegistersStale();
        InstructionClockStatus = "Avanzamento fino alla prossima istruzione…";
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
            // No automatic dumps inside a burst: every pulse belongs to the
            // program and the stop remains at T1, before the next opcode read.
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
                FinishInstructionClock($"Confine istruzione raggiunto · {count} · clock fermo.");
            else
            {
                InstructionClockStatus = $"Avanzamento in corso · {count}…";
                SendNextInstructionClockPulse();
            }
        }
        catch (InvalidOperationException ex) { FinishInstructionClock(ex.Message); }
    }

    [RelayCommand(CanExecute = nameof(IsInstructionClockRunning))]
    private void StopInstructionClock()
    {
        // Await the pulse already sent; never truncate it or send another one.
        _instructionClockStopRequested = true;
        InstructionClockStatus = "Arresto al termine dell'impulso in corso…";
    }

    private void FinishInstructionClock(string status)
    {
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = status;
        TryReadInstruction();
    }

    private void CancelInstructionClock()
    {
        _instructionClockStepper = null;
        _instructionClockPulseSender = null;
        _instructionClockStopRequested = false;
        IsInstructionClockRunning = false;
        InstructionClockStatus = string.Empty;
    }
}
