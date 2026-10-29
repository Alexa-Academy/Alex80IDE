using Alex80_IDE.Models;

internal static class ClockTraceTrackerTests
{
    private static LogElement L(uint clock, byte control, byte flags, ushort pc = 0x2000, bool manual = true) =>
        new(pc, 0x3C, (control & 32) != 0, (control & 16) != 0, (control & 8) != 0,
            (control & 4) != 0, (control & 2) != 0, (control & 1) != 0, "",
            (control & 64) != 0, (control & 128) != 0, clock, flags, manual);

    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}"); if (!ok) failures++; }
        (ClockTraceTracker Trace, LogElement Pulse) Pause(uint first = 0)
        {
            var trace = new ClockTraceTracker();
            trace.Observe(L(first, 0x3F, 0));
            trace.Observe(L(unchecked(first + 1), 0x3E, 4));
            var pulse = L(unchecked(first + 2), 0x1A, 5);
            trace.Observe(pulse);
            return (trace, pulse);
        }

        foreach (var logging in new[] { "off", "compact", "full", "full con buchi interni" })
        {
            var (trace, pulse) = Pause();
            if (logging.StartsWith("full"))
                for (uint clock = 3; clock <= 132; ++clock)
                    if (logging == "full" || clock % 2 == 0) trace.Observe(L(clock, 0x5A, 2, manual: false));
            trace.ConfirmManualRegisterRead(pulse);
            var released = L(133, 0x1A, 4); trace.Observe(released);
            var t3 = L(134, 0x3D, 0); trace.Observe(t3);
            var t4 = L(135, 0x39, 0); trace.Observe(t4);
            trace.Observe(L(136, 0x3E, 4, 0x2001));
            Check("lettura registri " + logging + ": ripresa T3/T4 verificata", pulse.TStateText == "T2" &&
                released.TStateText == "Tw1" && t3.TStateText == "T3" && t4.TStateText == "T4");
            Check("lettura registri " + logging + ": clock debugger contati una sola volta",
                trace.Summary.Contains("4 T programma · 1 WAIT · 130 clock debugger"));
        }

        // The 32-bit physical clock wraps naturally; no absolute ordering test.
        var (wrapped, origin) = Pause(uint.MaxValue - 2);
        wrapped.ConfirmManualRegisterRead(origin);
        var afterWrap = L(130, 0x1A, 4); wrapped.Observe(afterWrap);
        var afterWrapT3 = L(131, 0x3D, 0); wrapped.Observe(afterWrapT3);
        Check("lettura registri: rollover contatore fisico", afterWrap.TStateText == "Tw1" && afterWrapT3.TStateText == "T3");

        // Real program gaps and unrelated completions must remain unknown.
        foreach (var invalid in new[] { "manca conferma", "altro impulso", "altro indirizzo", "log automatico", "troppi clock", "reset", "errore", "legacy" })
        {
            var (trace, pulse) = Pause();
            if (invalid != "manca conferma") trace.ConfirmManualRegisterRead(invalid == "altro impulso" ? L(2, 0x1A, 5) : pulse);
            if (invalid == "reset") trace.Reset();
            if (invalid == "errore") trace.InvalidateSynchronization();
            if (invalid == "legacy") trace.Observe(new LogElement(0, 0, true, true, true, true, true, true, ""));
            var next = L(invalid == "troppi clock" ? 600u : 133u, 0x1A, 4,
                invalid == "altro indirizzo" ? (ushort)0x3000 : (ushort)0x2000, invalid != "log automatico");
            trace.Observe(next);
            Check("lettura registri: nessun conteggio inventato con " + invalid, next.TStateText.Contains('?'));
        }

        var (lost, held) = Pause();
        lost.ConfirmManualRegisterRead(held);
        lost.Observe(L(133, 0x1A, 4));
        var missing = L(136, 0x39, 0); lost.Observe(missing);
        Check("buco nel programma dopo la ripresa resta sconosciuto", missing.TStateText == "T?");

        var partial = new ClockTraceTracker(); var partialPulse = L(2, 0x1A, 5);
        partial.Observe(partialPulse); partial.ConfirmManualRegisterRead(partialPulse);
        var partialNext = L(133, 0x1A, 4); partial.Observe(partialNext);
        Check("lettura registri non inventa l'inizio di una traccia parziale", partialNext.TStateText == "Tw?");

        // OUT is 11 nominal T-states: the release clock is WAIT, not a 12th T.
        var (output, outPulse) = Pause(); output.ConfirmManualRegisterRead(outPulse);
        output.Observe(L(133, 0x1A, 4));
        for (uint t = 3; t <= 11; ++t) output.Observe(L(131 + t, 0x3F, 0));
        output.Observe(L(143, 0x3E, 4, 0x2002));
        Check("OUT conserva 11 T nominali con lettura registri", output.Summary.Contains("11 T programma · 1 WAIT · 130 clock debugger"));
        return failures;
    }
}
