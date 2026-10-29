using System;

namespace Alex80_IDE.Models;

// Interpret clock-numbered samples, anchoring at an observed first T-state
// and bridging only a confirmed manual register read. Compact traces can omit WAIT.
public sealed class ClockTraceTracker
{
    private LogElement? previous;
    private bool synced;
    private int t, waits, debug;
    private uint address;
    private string lastProgram = "";
    private RegisterPause? registerPause, confirmedPause;
    private sealed record RegisterPause(LogElement Row, bool Synced, int T, int Waits,
        int Debug, uint Address, string LastProgram, string Summary);
    public string Summary { get; private set; } = "Nessun intervallo completo misurato.";
    public void Reset()
    { previous = null; InvalidateSynchronization(); t = waits = debug = 0; lastProgram = ""; Summary = "Nessun intervallo completo misurato."; }

    public void InvalidateSynchronization()
    { synced = false; registerPause = confirmedPause = null; }

    // Only the successful PULSE -> matching DUMP -> PULSEEND transaction can
    // authorize bridging a gap. An arbitrary dump or a clock gap cannot do so.
    public void ConfirmManualRegisterRead(LogElement pulse)
    {
        if (registerPause?.Row == pulse) confirmedPause = registerPause;
    }

    private bool TryResumeAfterRegisterRead(LogElement row)
    {
        var pause = confirmedPause;
        confirmedPause = null;
        if (pause is null || !row.IsManualPulse || row.IsInjected || row.IsReset ||
            row.HardwareHalt != false || row.Waiting != false || !row.IsInstructionFetch ||
            row.Address != pause.Row.Address || row.ClockNumber is not { } clock) return false;
        uint distance = unchecked(clock - pause.Row.ClockNumber!.Value);
        // The manager bounds a manual injection to 512 clocks. The next pulse
        // is one additional clock, with the CPU still at the original fetch.
        if (distance < 2 || distance > 513) return false;
        previous = pause.Row;
        synced = pause.Synced; t = pause.T; waits = pause.Waits;
        address = pause.Address; lastProgram = pause.LastProgram;
        debug = pause.Debug + (int)distance - 1;
        Summary = lastProgram == "" ? pause.Summary : lastProgram + $" · {debug} clock debugger";
        return true;
    }

    private void RememberRegisterPause(LogElement row)
    {
        registerPause = row.IsManualPulse && row.Waiting == true &&
            row.StartsInstruction == true && row.IsInstructionFetch && row.ClockNumber.HasValue
            ? new(row, synced, t, waits, debug, address, lastProgram, Summary) : null;
    }
    public void Observe(LogElement row)
    {
        row.ObservedPhase = !row.Z80_RFSH ? "Refresh" : !row.Z80_IORQ ? "I/O" : row.IsInstructionFetch ? "Fetch" : !row.Z80_MREQ ? "Memoria" : "Interno";
        if (row.ClockNumber is null || row.Waiting is null)
        { row.TStateText = "—"; InvalidateSynchronization(); previous = row; return; }
        bool consecutive = TryResumeAfterRegisterRead(row) ||
            previous?.ClockNumber is { } clock && unchecked(row.ClockNumber.Value - clock) == 1;
        if (!consecutive) synced = false;
        if (row.IsReset || row.HardwareHalt == true)
        { row.TStateText = "—"; InvalidateSynchronization(); previous = row; return; }
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
            InvalidateSynchronization(); row.TStateText = "—"; row.ObservedPhase = "Acknowledge interrupt";
            if (previous is { Z80_M1: false }) { previous.TStateText = "—"; previous.ObservedPhase = "Acknowledge interrupt"; }
            previous = row; return;
        }
        // Before T1's falling edge M1 is already low, while RD/MREQ are still
        // high (Zilog UM0080, figure 5). This identifies T1 even if recording
        // starts here or earlier samples were omitted. StartsInstruction alone
        // also remains set throughout T2/WAIT and must not restart the counter.
        bool firstTState = !row.Z80_M1 && row.Z80_IORQ && row.Z80_RFSH &&
                           row.Z80_RD && row.Z80_WR && row.Z80_MREQ &&
                           row.Waiting == false && row.StartsInstruction == true;
        bool newInstruction = firstTState ||
                              (consecutive && previous is { Z80_M1: true } &&
                               !row.Z80_M1 && row.Z80_IORQ && row.StartsInstruction == true);
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
            RememberRegisterPause(row); previous = row; return;
        }
        else if (synced) t++;
        row.TStateText = synced ? $"T{t}" : row.Waiting == true ? "Tw?" : "T?";
        RememberRegisterPause(row);
        previous = row;
    }
}
