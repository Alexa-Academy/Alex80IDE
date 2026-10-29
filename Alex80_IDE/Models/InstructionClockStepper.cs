using System;

namespace Alex80_IDE.Models;

// Advance from the current bus position to the next instruction's T1. A nominal
// duration cannot decide conditional branches, repeated blocks or prefix chains.
// Each observation must acknowledge exactly one requested, non-injected pulse.
public sealed class InstructionClockStepper
{
    private uint? previousClock;
    public int Pulses { get; private set; }

    public bool Observe(LogElement sample)
    {
        if (!sample.IsManualPulse || sample.ClockNumber is not { } clock ||
            sample.Waiting is null || sample.StartsInstruction is null)
            throw new InvalidOperationException("Campione del clock manuale non valido.");
        if (previousClock is { } previous && unchecked(clock - previous) != 1)
            throw new InvalidOperationException("Sequenza di clock incompleta: avanzamento interrotto.");
        previousClock = clock;
        Pulses++;
        if (sample.IsReset)
            throw new InvalidOperationException("Reset rilevato: avanzamento interrotto.");
        if (sample.HardwareHalt == true)
            throw new InvalidOperationException("CPU in HALT: avanzamento interrotto.");
        if (sample.IsInjected || sample.Waiting == true)
            throw new InvalidOperationException("Intervento del debugger rilevato: avanzamento interrotto.");
        return sample.IsInstructionStartTState;
    }
}
