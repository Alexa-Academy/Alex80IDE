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

        foreach (bool manual in new[] { false, true })
        {
            var trace = new ClockTraceTracker();
            var t1 = L(100, 0x3E, 4, manual: manual);
            var t2 = L(101, 0x1A, 4, manual: manual);
            var t3 = L(102, 0x3D, 0, manual: manual);
            var t4 = L(103, 0x39, 0, manual: manual);
            foreach (var row in new[] { t1, t2, t3, t4 }) trace.Observe(row);
            trace.Observe(L(104, 0x3E, 4, 0x2001, manual));
            string mode = manual ? "manuale senza log continuo" : "continuo";
            Check("T1 iniziale riconosciuto nel log " + mode,
                t1.TStateText == "T1" && t2.TStateText == "T2" &&
                t3.TStateText == "T3" && t4.TStateText == "T4" &&
                trace.Summary.Contains("4 T programma · 0 WAIT"));

            var recovered = L(200, 0x3E, 4, manual: manual);
            trace.Observe(recovered);
            Check("T1 risincronizza dopo campioni omessi nel log " + mode, recovered.TStateText == "T1");

            var partialFetch = L(300, 0x1A, 4, manual: manual);
            trace.Observe(partialFetch);
            Check("fetch gia' iniziato resta incerto nel log " + mode, partialFetch.TStateText == "T?");
            var heldFetch = L(400, 0x1A, 5, manual: manual);
            trace.Observe(heldFetch);
            Check("WAIT iniziale resta incerto nel log " + mode, heldFetch.TStateText == "Tw?");

            // M1 also identifies prefix fetches; only the firmware's first-opcode
            // flag permits starting the instruction counter again.
            trace.Observe(L(500, 0x3E, 4, manual: manual));
            trace.Observe(L(501, 0x1A, 4, manual: manual));
            trace.Observe(L(502, 0x3D, 0, manual: manual));
            trace.Observe(L(503, 0x39, 0, manual: manual));
            var prefix = L(504, 0x3E, 0, 0x2001, manual);
            trace.Observe(prefix);
            Check("fetch di prefisso continua a T5 nel log " + mode, prefix.TStateText == "T5");
            var lostPrefix = L(600, 0x3E, 0, 0x2001, manual);
            trace.Observe(lostPrefix);
            Check("prefisso isolato non inventa T1 nel log " + mode, lostPrefix.TStateText == "T?");
        }

        foreach (var logging in new[] { "off", "compact", "full", "full con buchi interni" })
        {
            var (trace, pulse) = Pause();
            if (logging.StartsWith("full"))
                for (uint clock = 3; clock <= 132; ++clock)
                    if (logging == "full" || clock % 2 == 0) trace.Observe(L(clock, 0x5A, 2, manual: false));
            trace.ConfirmManualRegisterRead(pulse);
            Check("lettura registri " + logging + ": posizione disponibile prima del nuovo step",
                trace.CurrentTState == 2 && trace.CurrentInstructionAddress == 0x2000);
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
