using System.Reflection;
using Alex80_IDE;
using Alex80_IDE.InstructionReference;
using Alex80_IDE.Models;
using Avalonia.Threading;

internal static class InstructionClockStepperTests
{
    private static LogElement Pulse(uint clock, byte control = 0x3F, byte flags = 0, ushort pc = 0x2000) =>
        new(pc, 0, (control & 32) != 0, (control & 16) != 0, (control & 8) != 0,
            (control & 4) != 0, (control & 2) != 0, (control & 1) != 0, "",
            (control & 64) != 0, (control & 128) != 0, clock, flags, manualPulse: true);
    private static InstructionInfo Decode(params byte[] bytes) => InstructionDecoder.Decode(0x2000, bytes);

    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} clock per istruzione: {name}"); if (!ok) failures++; }
        foreach (var (bytes, duration, f, b) in new[] {
            (new byte[] { 0x00 }, 4, (byte?)null, (byte?)null),
            (new byte[] { 0x3E, 1 }, 7, null, null),
            (new byte[] { 0x20, 0xFE }, 7, (byte?)0x40, null),
            (new byte[] { 0x20, 0xFE }, 12, (byte?)0, null),
            (new byte[] { 0xCD, 0, 0x10 }, 17, null, null),
            (new byte[] { 0xC0 }, 11, (byte?)0, null),
            (new byte[] { 0xC0 }, 5, (byte?)0x40, null),
            (new byte[] { 0x10, 0xFE }, 13, null, (byte?)2),
            (new byte[] { 0x10, 0xFE }, 8, null, (byte?)1),
            (new byte[] { 0xD3, 0 }, 11, null, null),
            (new byte[] { 0xDD, 0xCB, 0, 6 }, 23, null, null) })
        {
            var instruction = Decode(bytes);
            var stepper = new InstructionClockStepper();
            stepper.SetInstruction(instruction, 1, 100, f, b);
            bool premature = false, complete = false;
            for (int t = 2; t <= duration; ++t)
            {
                byte control = t == 2 ? (byte)0x1A : t % 4 == 1 ? (byte)0x3E : (byte)0x3F;
                complete = stepper.Observe(Pulse((uint)(99 + t), control, t == 2 ? (byte)4 : (byte)0));
                premature |= complete && t < duration;
            }
            Check(instruction.Mnemonic + $": arresto a T{duration}, prima del nuovo T1",
                complete && !premature && stepper.Pulses == duration - 1 && stepper.ElapsedTStates == duration);
        }
        var partial = new InstructionClockStepper(); partial.SetInstruction(Decode(0), 3, 20);
        Check("da T3 di NOP emette solo T4", partial.Observe(Pulse(21, 0x39)) && partial.Pulses == 1);
        var next = new InstructionClockStepper(); next.SetInstruction(Decode(0), 4, 21);
        Check("dopo T4 attende i nuovi byte a T1", !next.Observe(Pulse(22, 0x3E, 4)) && next.NeedsInstruction && next.Pulses == 1);
        next.SetInstruction(Decode(0x3E, 1), 1, 22);
        bool ended = false;
        for (uint clock = 23; clock <= 28; clock++) ended = next.Observe(Pulse(clock));
        Check("nuova istruzione da sette T: esattamente sette impulsi", ended && next.Pulses == 7);
        var wrapped = new InstructionClockStepper(); wrapped.SetInstruction(Decode(0), 3, uint.MaxValue);
        Check("rollover del contatore fisico", wrapped.Observe(Pulse(0, 0x39)));
        foreach (var instruction in new[] { Decode(0x20, 0xFE), Decode(0x10, 0xFE), Decode(0xED, 0xB0), Decode(0xDD) })
        {
            bool rejected = false;
            try { new InstructionClockStepper().SetInstruction(instruction, 1, 1); }
            catch (InvalidOperationException) { rejected = true; }
            Check(instruction.Mnemonic + ": durata ignota non indovinata", rejected);
        }
        foreach (var (name, invalid) in new[] {
            ("clock mancante", Pulse(3)), ("clock duplicato", Pulse(1)),
            ("HALT", Pulse(2, 0x1A, 8)), ("reset", Pulse(2, 0xBE, 4)),
            ("iniezione", Pulse(2, 0x7E, 4)), ("WAIT debugger", Pulse(2, 0x1A, 5)),
            ("interrupt acknowledge", Pulse(2, 0x36)), ("T1 anticipato", Pulse(2, 0x3E, 4)),
            ("legacy", new LogElement(0, 0, true, true, true, true, true, true, "")) })
        {
            var stepper = new InstructionClockStepper(); stepper.SetInstruction(Decode(0), 1, 1);
            bool rejected = false;
            try { stepper.Observe(invalid); }
            catch (InvalidOperationException) { rejected = true; }
            Check("interrompe su " + name, rejected);
        }
        TestViewModel(Check);
        return failures;
    }

    private static void TestViewModel(Action<string, bool> check)
    {
        var vm = new MainViewModel(new NoDialogs());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        void Vm(string field, object value) => typeof(MainViewModel).GetField(field, flags)!.SetValue(vm, value);
        void Serial(string field, object value) => typeof(SerialManager).GetField(field, flags)!.SetValue(manager, value);
        void Rx(string frame)
        {
            typeof(SerialManager).GetMethod("HandleSerialMessage", flags)!.Invoke(manager, new object[] { frame });
            Dispatcher.UIThread.RunJobs();
        }
        void Info(string settings = "CLKPULSE:ON,ECLK:0,DEBUG:OFF,SCLK:A") =>
            Rx("!INFO:CARD:ALEX80µ,SMEM:A,LOG:OFF," + settings + ";");
        int sent = 0, reads = 0;
        uint readPc = 0;
        void Begin()
        {
            Func<bool> send = () =>
            {
                if (manager.IsClockPulsePending || manager.IsReadPending) throw new InvalidOperationException("Operazioni sovrapposte");
                sent++;
                Serial("clockPulsePending", true); Serial("clockPulseRegisters", false);
                return true;
            };
            Func<uint, bool> read = pc =>
            {
                if (manager.IsClockPulsePending || manager.IsReadPending) throw new InvalidOperationException("Lettura sovrapposta");
                reads++; readPc = pc;
                ((List<byte>)typeof(SerialManager).GetField("instructionBytes", flags)!.GetValue(manager)!).Clear();
                var purpose = typeof(SerialManager).GetField("readPurpose", flags)!;
                purpose.SetValue(manager, Enum.Parse(purpose.FieldType, "Instruction"));
                Serial("instructionAddress", pc); Serial("instructionChunkAddress", pc);
                Serial("instructionWrapMask", 0xFFFFu); Serial("instructionChunkLength", 16);
                return true;
            };
            typeof(MainViewModel).GetMethod("StartInstructionClock", flags)!.Invoke(vm, new object[] { send, read });
        }
        void ReceivePulse(uint clock, byte control, byte trace, ushort pc = 0x2000)
        {
            byte[] payload = { (byte)(pc >> 8), (byte)pc, 0, control, trace,
                (byte)(clock >> 24), (byte)(clock >> 16), (byte)(clock >> 8), (byte)clock };
            Rx("!PULSE" + Convert.ToBase64String(payload) + ";");
        }
        void Bytes(params byte[] bytes)
        {
            var payload = new byte[16]; bytes.CopyTo(payload, 0);
            Rx("!STATUS1;"); Rx("!DATA" + Convert.ToBase64String(payload) + ";"); Rx("!STATUS2;");
        }
        void Prepare(uint clock, params byte[] bytes)
        {
            ReceivePulse(clock, 0x3E, 4);
            vm.CurrentInstruction = Decode(bytes); Vm("_instructionNeedsRead", false);
        }
        try
        {
            Vm("_autoRefreshRegisters", true);
            check("disabilitato senza scheda", !vm.AdvanceInstructionClockCommand.CanExecute(null));
            vm.IsSerialOpened = true; Info();
            check("clock singolo non promette dump o WAIT anche con registri automatici attivi",
                vm.ClockPulseToolTip.Contains("senza WAIT") && vm.AutoRefreshRegisters && vm.CanPulseClock);
            foreach (var settings in new[] { "ECLK:1,DEBUG:OFF,SCLK:A,CLKPULSE:ON",
                "ECLK:0,DEBUG:ON,SCLK:A,CLKPULSE:ON", "ECLK:0,DEBUG:OFF,SCLK:E,CLKPULSE:ON",
                "ECLK:0,DEBUG:OFF,SCLK:A" })
            {
                Info(settings);
                check("requisiti rispettati: " + settings, !vm.AdvanceInstructionClockCommand.CanExecute(null));
            }
            Info(); vm.AdvanceInstructionClockCommand.Execute(null);
            check("invio impossibile sblocca la UI", !vm.IsInstructionClockRunning && vm.InstructionClockStatus.Contains("Impossibile"));

            Prepare(10, 0);
            vm.StepCommand.Execute(null);
            check("Step con clock Arduino fermo usa il conteggio, senza invalidare l'istruzione per inviare s",
                !vm.IsInstructionClockRunning && vm.InstructionClockStatus.Contains("Impossibile") && vm.CurrentInstruction?.Mnemonic == "NOP");
            int before = sent; Begin();
            ReceivePulse(11, 0x1A, 4); ReceivePulse(12, 0x3D, 0); ReceivePulse(13, 0x39, 0);
            check("NOP su T1: tre impulsi e ultimo log T4, nessun T1 successivo", sent == before + 3 &&
                !vm.IsInstructionClockRunning && !manager.IsClockPulsePending && !vm.IsClockActive &&
                vm.LogTable.TakeLast(4).Select(row => row.TStateText).SequenceEqual(new[] { "T1", "T2", "T3", "T4" }) &&
                vm.InstructionClockStatus.Contains("prima del prossimo T1"));

            before = sent; Begin(); ReceivePulse(14, 0x3E, 4, 0x2001);
            check("nuovo clic: un T1 poi lettura memoria a clock fermo", sent == before + 1 && reads == 1 &&
                readPc == 0x2001 && manager.IsReadPending && !manager.IsClockPulsePending && vm.IsInstructionClockRunning);
            Bytes(0x3E, 1);
            check("byte ricevuti sbloccano solo il prossimo impulso", sent == before + 2 && manager.IsClockPulsePending &&
                vm.CurrentInstruction.Mnemonic == "LD A,01h");
            ReceivePulse(15, 0x1A, 4, 0x2001); ReceivePulse(16, 0x3D, 0, 0x2001);
            ReceivePulse(17, 0x39, 0, 0x2001); ReceivePulse(18, 0x3F, 0);
            ReceivePulse(19, 0x1B, 0); ReceivePulse(20, 0x3F, 0);
            check("istruzioni consecutive: sette impulsi fino a T7, nessun WAIT", !vm.IsInstructionClockRunning &&
                sent == before + 7 && vm.LogTable[^1].TStateText == "T7" && vm.LogTable.All(row => row.Waiting == false));

            Prepare(100, 0x20, 0xFE); before = sent; Begin();
            check("JR con flag obsoleti non invia alcun impulso", !vm.IsInstructionClockRunning && sent == before &&
                vm.InstructionClockStatus.Contains("Durata variabile"));
            Vm("_registersAreCurrent", true); vm.PcReg = 0x2000; vm.FReg = 0x40;
            Begin();
            for (uint clock = 101; clock <= 106; clock++) ReceivePulse(clock, 0x3F, 0);
            check("JR non preso: flag aggiornati selezionano sette T", !vm.IsInstructionClockRunning && sent == before + 6);

            before = sent; Begin(); ReceivePulse(107, 0x3E, 4, 0x2002); Bytes(0);
            ReceivePulse(108, 0x1A, 4, 0x2002); ReceivePulse(109, 0x3D, 0); ReceivePulse(110, 0x39, 0);
            check("dopo JR completato pianifica la nuova istruzione senza riusare i vecchi flag",
                !vm.IsInstructionClockRunning && sent == before + 4);

            Prepare(200, 0); Begin();
            check("comandi concorrenti bloccati", !vm.PulseClockCommand.CanExecute(null) && !vm.ReadMemoryCommand.CanExecute(null) &&
                !vm.StepCommand.CanExecute(null) && !vm.CanRun && !vm.StartStopBoardCommand.CanExecute(null));
            vm.StopInstructionClockCommand.Execute(null); before = sent;
            check("ferma attende il solo impulso gia' inviato", vm.IsInstructionClockRunning && manager.IsClockPulsePending);
            ReceivePulse(201, 0x1A, 4);
            check("ferma non aggiunge impulsi", !vm.IsInstructionClockRunning && sent == before && !manager.IsClockPulsePending);

            Prepare(300, 0); ReceivePulse(301, 0x1A, 4); ReceivePulse(302, 0x3D, 0); ReceivePulse(303, 0x39, 0);
            Begin(); ReceivePulse(304, 0x3E, 4, 0x2001); before = sent;
            vm.StopInstructionClockCommand.Execute(null); Bytes(0);
            check("ferma durante la lettura non riparte alla risposta", !vm.IsInstructionClockRunning && sent == before);

            Prepare(400, 0); Begin(); Rx("!ERROR:TEST FAILURE;");
            check("errore seriale interrompe", !vm.IsInstructionClockRunning && !manager.IsClockPulsePending);
            Prepare(500, 0); Begin(); ReceivePulse(501, 0x1A, 8);
            check("HALT interrompe", !vm.IsInstructionClockRunning && vm.InstructionClockStatus.Contains("HALT"));
            Prepare(600, 0); Begin(); vm.ResetCommand.Execute(null); Dispatcher.UIThread.RunJobs(); before = sent;
            ReceivePulse(601, 0x3F, 0);
            check("reset impedisce riprese tardive", !vm.IsInstructionClockRunning && sent == before && vm.InstructionClockStatus == "");
            Prepare(650, 0);
            Info("CLKPULSE:ON,ECLK:0,DEBUG:ON,SCLK:A");
            check("clock Arduino fermo non invia Step WAIT se Debug e' attivo", !vm.StepCommand.CanExecute(null));
            Info("CLKPULSE:ON,ECLK:0,DEBUG:ON,SCLK:E");
            check("cambio a clock esterno elimina il conteggio precedente e mantiene Step WAIT",
                vm.CurrentInstruction is null && !vm.CanPulseClock && vm.StepCommand.CanExecute(null) && vm.StepToolTip.Contains("WAIT"));
            Info();
            Prepare(700, 0); Begin(); vm.IsSerialOpened = false; before = sent;
            ReceivePulse(701, 0x3E, 4);
            check("disconnessione impedisce riprese tardive", !vm.IsInstructionClockRunning && sent == before);
            vm.IsSerialOpened = true; Rx("!INFO:CARD:eZ80Test,SCLK:E,ECLK:0,SMEM:E;");
            check("eZ80 mantiene i controlli ZDI", !vm.AdvanceInstructionClockCommand.CanExecute(null));
        }
        finally
        {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.CancelReadForReset(); manager.SerialPort.Dispose();
        }
    }
    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
