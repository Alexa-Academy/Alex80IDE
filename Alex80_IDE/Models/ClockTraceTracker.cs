using System;

namespace Alex80_IDE.Models;

// Interpret only consecutive, clock-numbered samples. A table row index is not
// a clock counter: compact/legacy traces can omit arbitrary stretches of WAIT.
public sealed class ClockTraceTracker
{
    private LogElement? previous;
    private bool synced;
    private int t, waits, debug;
    private uint address;
    private string lastProgram = "";
    public string Summary { get; private set; } = "Nessun intervallo completo misurato.";
    public void Reset()
    { previous = null; synced = false; t = waits = debug = 0; lastProgram = ""; Summary = "Nessun intervallo completo misurato."; }
    public void Observe(LogElement row)
    {
        row.ObservedPhase = !row.Z80_RFSH ? "Refresh" : !row.Z80_IORQ ? "I/O" : row.IsInstructionFetch ? "Fetch" : !row.Z80_MREQ ? "Memoria" : "Interno";
        if (row.ClockNumber is null || row.Waiting is null)
        { row.TStateText = "—"; synced = false; previous = row; return; }
        bool consecutive = previous?.ClockNumber is { } clock && unchecked(row.ClockNumber.Value - clock) == 1;
        if (!consecutive) synced = false;
        if (row.IsReset || row.HardwareHalt == true)
        { row.TStateText = "—"; synced = false; previous = row; return; }
        if (row.IsInjected)
        {
            debug++;
            if (lastProgram != "") Summary = lastProgram + $" · {debug} clock debugger";
            // The next opcode's first T-state can precede automatic injection.
            // It has not executed a program instruction: do not count it as one.
            synced = false; row.TStateText = "—"; previous = row; return;
        }
        if (!row.Z80_M1 && !row.Z80_IORQ)
        {
            synced = false; row.TStateText = "—"; row.ObservedPhase = "Acknowledge interrupt";
            if (previous is { Z80_M1: false }) { previous.TStateText = "—"; previous.ObservedPhase = "Acknowledge interrupt"; }
            previous = row; return;
        }
        bool newInstruction = consecutive && previous is { Z80_M1: true } &&
                              !row.Z80_M1 && row.Z80_IORQ && row.StartsInstruction == true;
        if (newInstruction)
        {
            if (synced)
            {
                lastProgram = $"Ultimo intervallo da {address:X4}: {t} T programma · {waits} WAIT";
                Summary = lastProgram + (debug > 0 ? $" · {debug} clock debugger" : "");
                debug = 0;
            }
            synced = true; address = row.Address; t = 1; waits = 0;
        }
        else if (consecutive && previous?.Waiting == true && !row.Z80_M1 &&
                 previous.Address == row.Address && !previous.IsInjected)
        {
            waits++; row.TStateText = synced ? $"Tw{waits}" : "Tw?";
            if (row.Waiting == false) row.ObservedPhase = "Fine WAIT";
            previous = row; return;
        }
        else if (synced) t++;
        row.TStateText = synced ? $"T{t}" : row.Waiting == true ? "Tw?" : "T?";
        previous = row;
    }
}
