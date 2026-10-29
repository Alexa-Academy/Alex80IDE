using System.Reflection;
using Alex80_IDE;
using Alex80_IDE.Models;
using Avalonia.Threading;

internal static class InstructionClockStepperTests
{
    private static LogElement Pulse(uint clock, byte control = 0x3F, byte flags = 0, ushort pc = 0x2000) =>
        new(pc, 0, (control & 32) != 0, (control & 16) != 0, (control & 8) != 0,
            (control & 4) != 0, (control & 2) != 0, (control & 1) != 0, "",
            (control & 64) != 0, (control & 128) != 0, clock, flags, manualPulse: true);

    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} clock per istruzione: {name}"); if (!ok) failures++; }

        // Start with the CPU already on T1. Confirm that only the next real
        // boundary stops the burst, independently of a nominal timing lookup.
        foreach (var (name, duration) in new[] {
            ("NOP", 4), ("LD A,n", 7), ("JR non preso", 7), ("JR preso", 12),
            ("CALL", 17), ("prefissi DD CB", 23), ("blocco ripetuto", 42) })
        {
            var stepper = new InstructionClockStepper();
            bool premature = false;
            for (uint t = 2; t <= duration; ++t)
            {
                // The first opcode's T2 still has StartsInstruction set. Prefix
                // and block-continuation fetches have M1 low but that flag clear.
                byte control = t == 2 ? (byte)0x1A : t % 4 == 1 ? (byte)0x3E : (byte)0x3F;
                premature |= stepper.Observe(Pulse(t, control, t == 2 ? (byte)4 : (byte)0));
            }
            bool complete = stepper.Observe(Pulse((uint)duration + 1, 0x3E, 4));
            Check(name + ": durata effettiva e arresto sul T1 successivo",
                !premature && complete && stepper.Pulses == duration);
        }

        var partial = new InstructionClockStepper();
        Check("da meta' istruzione completa solo i clock restanti",
            !partial.Observe(Pulse(20, 0x3D)) && !partial.Observe(Pulse(21, 0x39)) &&
            partial.Observe(Pulse(22, 0x3E, 4)) && partial.Pulses == 3);
        var unknown = new InstructionClockStepper();
        Check("posizione ignota: il primo T1 e' il confine", unknown.Observe(Pulse(100, 0x3E, 4)) && unknown.Pulses == 1);
        var wrapped = new InstructionClockStepper();
        Check("rollover del contatore fisico", !wrapped.Observe(Pulse(uint.MaxValue, 0x39)) &&
            wrapped.Observe(Pulse(0, 0x3E, 4)));

        foreach (var (name, invalid) in new[] {
            ("clock mancante", Pulse(3)), ("clock duplicato", Pulse(1)),
            ("HALT", Pulse(2, 0x1A, 8)), ("reset", Pulse(2, 0xBE, 4)),
            ("iniezione", Pulse(2, 0x7E, 4)), ("WAIT debugger", Pulse(2, 0x1A, 5)),
            ("legacy", new LogElement(0, 0, true, true, true, true, true, true, "")) })
        {
            var stepper = new InstructionClockStepper(); stepper.Observe(Pulse(1));
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
        void Serial(string field, object value) => typeof(SerialManager).GetField(field, flags)!.SetValue(manager, value);
        void Rx(string frame)
        {
            typeof(SerialManager).GetMethod("HandleSerialMessage", flags)!.Invoke(manager, new object[] { frame });
            Dispatcher.UIThread.RunJobs();
        }
        void Info(string settings = "CLKPULSE:ON,ECLK:0,DEBUG:OFF,SCLK:A") =>
            Rx("!INFO:CARD:ALEX80µ,SMEM:A,LOG:OFF," + settings + ";");
        int sent = 0;
        void BeginPendingPulse()
        {
            Func<bool> sendPulse = () =>
            {
                sent++;
                Serial("clockPulsePending", true); Serial("clockPulseRegisters", false);
                return true;
            };
            typeof(MainViewModel).GetMethod("StartInstructionClock", flags)!.Invoke(vm, new object[] { sendPulse });
        }
        void ReceivePulse(uint clock, byte control, byte trace)
        {
            byte[] payload = { 0x20, 0, 0, control, trace,
                (byte)(clock >> 24), (byte)(clock >> 16), (byte)(clock >> 8), (byte)clock };
            Rx("!PULSE" + Convert.ToBase64String(payload) + ";");
        }
        try
        {
            check("disabilitato senza scheda", !vm.AdvanceInstructionClockCommand.CanExecute(null));
            vm.IsSerialOpened = true; Info();
            check("disponibile col clock fermo e senza log continui", !vm.LogEnabled &&
                vm.AdvanceInstructionClockCommand.CanExecute(null));
            foreach (var settings in new[] { "ECLK:1,DEBUG:OFF,SCLK:A,CLKPULSE:ON",
                "ECLK:0,DEBUG:ON,SCLK:A,CLKPULSE:ON", "ECLK:0,DEBUG:OFF,SCLK:E,CLKPULSE:ON",
                "ECLK:0,DEBUG:OFF,SCLK:A" })
            {
                Info(settings);
                check("requisiti rispettati: " + settings, !vm.AdvanceInstructionClockCommand.CanExecute(null));
            }
            Info();
            vm.AdvanceInstructionClockCommand.Execute(null);
            check("invio impossibile termina senza lasciare la UI occupata", !vm.IsInstructionClockRunning &&
                vm.InstructionClockStatus.Contains("Impossibile"));

            // Exercise real parser -> UI queue -> next request with a simulated
            // serial sender, so no physical board or user settings are needed.
            ReceivePulse(10, 0x3E, 4);
            int sentBefore = sent;
            BeginPendingPulse();
            check("avvio invia un solo impulso e attende la risposta", sent == sentBefore + 1 && manager.IsClockPulsePending);
            ReceivePulse(11, 0x1A, 4);
            check("T2 richiede esattamente il prossimo impulso", vm.IsInstructionClockRunning &&
                sent == sentBefore + 2 && manager.IsClockPulsePending);
            ReceivePulse(12, 0x3D, 0); ReceivePulse(13, 0x39, 0);
            check("refresh non conclude l'istruzione in anticipo", vm.IsInstructionClockRunning && sent == sentBefore + 4);
            ReceivePulse(14, 0x3E, 4);
            check("quattro T reali: nessun quinto impulso o clock debugger", !vm.IsInstructionClockRunning &&
                !manager.IsClockPulsePending && sent == sentBefore + 4 &&
                vm.InstructionClockStatus.Contains("4 colpi di clock") &&
                vm.LogTable.TakeLast(5).Select(row => row.TStateText).SequenceEqual(new[] { "T1", "T2", "T3", "T4", "T1" }));

            BeginPendingPulse();
            check("niente comandi concorrenti durante l'avanzamento", !vm.PulseClockCommand.CanExecute(null) &&
                !vm.AdvanceInstructionClockCommand.CanExecute(null) && !vm.ReadMemoryCommand.CanExecute(null) &&
                !vm.WriteMemoryCommand.CanExecute(null) && !vm.StepCommand.CanExecute(null) &&
                !vm.StartStopBoardCommand.CanExecute(null) && !vm.CanDumpRegisters && !vm.CanRun &&
                !vm.DebugCommand.CanExecute(null) && vm.StopInstructionClockCommand.CanExecute(null));
            ReceivePulse(1, 0x3E, 4);
            check("parser/UI: confine T1 ferma l'avanzamento senza attivare il clock automatico",
                !vm.IsInstructionClockRunning && !vm.IsClockActive && !manager.IsClockPulsePending &&
                vm.InstructionClockStatus.Contains("Confine istruzione raggiunto") && vm.LogTable[^1].TStateText == "T1");

            BeginPendingPulse();
            vm.StopInstructionClockCommand.Execute(null);
            sentBefore = sent;
            check("ferma attende la conferma dell'impulso gia' inviato", vm.IsInstructionClockRunning && manager.IsClockPulsePending);
            ReceivePulse(2, 0x1A, 4);
            check("ferma non richiede altri impulsi e conserva il campione finale", !vm.IsInstructionClockRunning &&
                !manager.IsClockPulsePending && sent == sentBefore && vm.InstructionClockStatus.Contains("interrotto · 1 colpo"));

            BeginPendingPulse(); Rx("!ERROR:TEST FAILURE;");
            check("errore seriale interrompe e sblocca i comandi", !vm.IsInstructionClockRunning &&
                !manager.IsClockPulsePending && vm.InstructionClockStatus.Contains("TEST FAILURE"));
            BeginPendingPulse(); ReceivePulse(3, 0x1A, 8);
            check("HALT termina senza cercare un'altra istruzione", !vm.IsInstructionClockRunning &&
                vm.InstructionClockStatus.Contains("HALT"));

            BeginPendingPulse(); vm.ResetCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            ReceivePulse(4, 0x3F, 0);
            check("reset cancella anche un impulso gia' accodato", !vm.IsInstructionClockRunning &&
                !manager.IsClockPulsePending && vm.InstructionClockStatus == "");
            BeginPendingPulse(); vm.IsSerialOpened = false;
            ReceivePulse(5, 0x3E, 4);
            check("disconnessione impedisce la ripresa da risposte tardive", !vm.IsInstructionClockRunning &&
                vm.InstructionClockStatus == "" && !vm.AdvanceInstructionClockCommand.CanExecute(null));
            vm.IsSerialOpened = true;
            Rx("!INFO:CARD:eZ80Test,SCLK:E,ECLK:0,SMEM:E;");
            check("eZ80 mantiene i comandi ZDI", !vm.AdvanceInstructionClockCommand.CanExecute(null));
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
