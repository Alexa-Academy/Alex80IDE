using System;
using Alex80_IDE.InstructionReference;

namespace Alex80_IDE.Models;

// CLK is under our control: stop on the instruction's final T-state, without
// entering the next T1 or holding WAIT. Unknown timings must not be guessed.
public sealed class InstructionClockStepper
{
    private uint? previousClock;
    private int? target;
    private LogElement? registerPause;
    public int Pulses { get; private set; }
    public int ElapsedTStates { get; private set; }
    public bool NeedsInstruction { get; private set; }

    public InstructionClockStepper(uint? clock = null, LogElement? registerPause = null)
    { previousClock = clock; this.registerPause = registerPause; }

    public void SetInstruction(InstructionInfo instruction, int elapsed, uint clock,
        byte? flags = null, byte? b = null)
    {
        int duration = ResolveDuration(instruction, flags, b);
        if (elapsed < 1 || elapsed > duration)
            throw new InvalidOperationException("Posizione T-state non valida: usare il clock singolo per risincronizzare.");
        previousClock = clock;
        NeedsInstruction = false;
        // The last instruction is already complete: the next requested pulse
        // will reach a new T1, where fresh opcode bytes are read with CLK low.
        target = elapsed == duration ? null : duration;
        ElapsedTStates = elapsed == duration ? 0 : elapsed;
    }

    public static int ResolveDuration(InstructionInfo instruction, byte? flags = null, byte? b = null)
    {
        if (!instruction.Complete)
            throw new InvalidOperationException("Istruzione non decodificata: avanzamento per clock non disponibile.");
        if (instruction.NominalTStates is > 0) return instruction.NominalTStates.Value;
        if (instruction.TakenTStates is { } taken && instruction.NotTakenTStates is { } notTaken)
        {
            var parts = instruction.Mnemonic.Split(' ', 2);
            bool? condition = parts[0] == "DJNZ" ? b is { } counter ? counter != 1 : null
                : flags is { } f && parts.Length == 2 ? parts[1].Split(',')[0] switch
                {
                    "NZ" => (f & 0x40) == 0, "Z" => (f & 0x40) != 0,
                    "NC" => (f & 1) == 0, "C" => (f & 1) != 0,
                    "PO" => (f & 4) == 0, "PE" => (f & 4) != 0,
                    "P" => (f & 0x80) == 0, "M" => (f & 0x80) != 0,
                    _ => (bool?)null
                } : null;
            if (condition is { } yes) return yes ? taken : notTaken;
        }
        throw new InvalidOperationException("Durata variabile non determinabile dai registri aggiornati: usare i clock singoli o il debug con WAIT.");
    }

    public bool Observe(LogElement sample)
    {
        if (!sample.IsManualPulse || sample.ClockNumber is not { } clock ||
            sample.Waiting is null || sample.StartsInstruction is null)
            throw new InvalidOperationException("Campione del clock manuale non valido.");
        bool resume = registerPause is { Waiting: true, StartsInstruction: true, IsInstructionFetch: true } pause && sample.IsInstructionFetch &&
            sample.Address == pause.Address && sample.Waiting == false &&
            pause.ClockNumber is { } pausedClock && unchecked(clock - pausedClock) is >= 2 and <= 513;
        if (registerPause is not null && !resume)
            throw new InvalidOperationException("Ripresa dopo la lettura dei registri non valida: avanzamento interrotto.");
        if (!resume && previousClock is { } previous && unchecked(clock - previous) != 1)
            throw new InvalidOperationException("Sequenza di clock incompleta: avanzamento interrotto.");
        previousClock = clock;
        Pulses++;
        if (sample.IsReset)
            throw new InvalidOperationException("Reset rilevato: avanzamento interrotto.");
        if (sample.HardwareHalt == true)
            throw new InvalidOperationException("CPU in HALT: avanzamento interrotto.");
        if (sample.IsInjected || sample.Waiting == true || (!sample.Z80_M1 && !sample.Z80_IORQ))
            throw new InvalidOperationException("Intervento del debugger rilevato: avanzamento interrotto.");
        if (resume)
        {
            registerPause = null;
            // This releases the held T2 (one WAIT), not a new program T-state.
            return false;
        }
        if (target is null)
        {
            if (!sample.IsInstructionStartTState)
                throw new InvalidOperationException("Posizione nell'istruzione sconosciuta: usare i clock singoli fino a T1.");
            ElapsedTStates = 1;
            NeedsInstruction = true;
            return false;
        }
        if (sample.IsInstructionStartTState)
            throw new InvalidOperationException("T1 inatteso: il bus non corrisponde al conteggio previsto.");
        return ++ElapsedTStates == target;
    }
}
