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
    [ObservableProperty] private InstructionInfo? _currentInstruction;
    [ObservableProperty] private string _instructionStatus = "Ferma la CPU e rileggi i registri per vedere l'istruzione al PC.";
    [ObservableProperty] private string _clockSummary = "Nessun intervallo completo misurato.";
    public bool HasInstruction => CurrentInstruction is not null;
    public string InstructionIdealTime => IsClockFromArduino ? CurrentInstruction?.IdealTime(int.TryParse(ClockFrequency, out var hz) ? hz : 0) ?? "" : "";
    public string InstructionPrediction
    {
        get
        {
            if (!_hasRegisterSnapshot || CurrentInstruction is not { } info) return "";
            var parts = info.Mnemonic.Split(' ', 2);
            if (parts.Length != 2 || parts[0] is not ("INC" or "DEC")) return "";
            byte? value = parts[1] switch { "A" => AReg, "B" => BReg, "C" => CReg, "D" => DReg, "E" => EReg, "H" => HReg, "L" => LReg, _ => null };
            return value is { } before ? $"Risultato previsto: {parts[1]} = {before:X2}h → {unchecked((byte)(before + (parts[0] == "INC" ? 1 : -1))):X2}h" : "";
        }
    }
    partial void OnCurrentInstructionChanged(InstructionInfo? value)
    {
        OnPropertyChanged(nameof(HasInstruction)); OnPropertyChanged(nameof(InstructionIdealTime));
        OnPropertyChanged(nameof(InstructionPrediction));
    }
    private void InitializeInstructionView()
    {
        _serialManager.InstructionBytesReceived += (pc, bytes) => Dispatcher.UIThread.Post(() =>
        {
            if (_instructionPc != pc) return;
            CurrentInstruction = InstructionDecoder.Decode(pc, bytes, IsEz80, IsAdl);
            InstructionStatus = "Prossima istruzione · byte letti dalla scheda";
        });
        _serialManager.InstructionReadFailed += (pc, message) => Dispatcher.UIThread.Post(() =>
        { if (_instructionPc == pc) { CurrentInstruction = null; InstructionStatus = message; } });
        _serialManager.ReadStateChanged += () => Dispatcher.UIThread.Post(() =>
        {
            ReadMemoryCommand.NotifyCanExecuteChanged(); WriteMemoryCommand.NotifyCanExecuteChanged();
            StepCommand.NotifyCanExecuteChanged();
            if (!_serialManager.IsReadPending) TryReadInstruction();
        });
    }
    private void RefreshStoppedInstruction(uint pc, bool force = false)
    {
        if (_instructionPc == pc && ((!force && CurrentInstruction is not null) || _serialManager.IsReadPending))
        { OnPropertyChanged(nameof(InstructionPrediction)); return; }
        _instructionPc = pc; _instructionNeedsRead = true;
        CurrentInstruction = null; InstructionStatus = $"PC {pc:X4} · lettura dell'istruzione…";
        TryReadInstruction();
    }
    private void TryReadInstruction()
    {
        if (!_instructionNeedsRead || _instructionPc is not { } pc || _serialManager.IsReadPending) return;
        if (_serialManager.ReadInstruction(pc, IsEz80, IsAdl)) _instructionNeedsRead = false;
        else InstructionStatus = $"PC {pc:X4} · byte dell'istruzione non disponibili.";
    }
    private void InvalidateInstruction()
    {
        _instructionPc = null; _instructionNeedsRead = false; CurrentInstruction = null;
        InstructionStatus = "Ferma la CPU e rileggi i registri per vedere l'istruzione al PC.";
    }
    private void ObserveInstructionLog(LogElement log)
    {
        _clockTrace.Observe(log); ClockSummary = _clockTrace.Summary;
        if (log.IsReset) InvalidateInstruction();
        else if (log.HardwareHalt == true) { InvalidateInstruction(); InstructionStatus = "CPU in HALT: eseguire reset per riprendere."; }
        else if (!log.IsInjected && log.Waiting == true && DebugEnabled) RefreshStoppedInstruction(log.Address);
    }
    private bool CanStepCpu() => IsBoardConnected && !_serialManager.IsReadPending;
    [RelayCommand]
    private void OpenInstructionManual()
    {
        if (CurrentInstruction is { } info) Process.Start(new ProcessStartInfo(info.ManualUrl) { UseShellExecute = true });
    }
}
