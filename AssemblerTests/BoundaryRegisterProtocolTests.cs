using System.Reflection;
using Alex80_IDE;
using Avalonia.Threading;

internal static class BoundaryRegisterProtocolTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool value)
        { Console.WriteLine($"{(value ? "ok  " : "FAIL")} registri al confine: {name}"); if (!value) failures++; }
        Check("capability opzionale", CardConfig.Parse("").RegisterBoundarySnapshotSupported is null &&
            CardConfig.Parse("REGBND:ON").RegisterBoundarySnapshotSupported == true);
        var vm = new MainViewModel(new NoDialogs());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        void Set(string field, object value) => typeof(SerialManager).GetField(field, flags)!.SetValue(manager, value);
        void Rx(string frame)
        {
            typeof(SerialManager).GetMethod("HandleSerialMessage", flags)!.Invoke(manager, new object[] { frame });
            Dispatcher.UIThread.RunJobs();
        }
        void Begin()
        {
            manager.CancelReadForReset();
            Set("clockPulsePending", true); Set("clockPulseRegisters", true);
            Set("boundaryRegisterStart", 42u); Set("boundaryRegisterDump", null);
        }
        byte[] dump = { 0x55, 1, 2, 3, 4, 5, 6, 0, 1, 0, 2, 0x80, 0, 0x40, 0x12, 0x34 };
        byte[] end = { 0x12, 0x34, 0, 0, 0, 42, 0, 0, 0, 136 };
        string Frame(string prefix, byte[] data) => prefix + Convert.ToBase64String(data) + ";";
        int confirmed = 0, published = 0;
        manager.BoundaryRegisterReadCompleted += (_, _, _) => confirmed++;
        manager.DumpReceived += (_, _, _, _, _, _, _, _, _, _, _, _) => published++;
        try
        {
            typeof(MainViewModel).GetField("_autoRefreshRegisters", flags)!.SetValue(vm, true);
            vm.IsSerialOpened = true;
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,SMEM:A,ECLK:0,DEBUG:OFF,CLKPULSE:ON,REGSNAP:ON,REGBND:ON;");
            Begin(); Rx(Frame("!BDUMP", dump));
            Check("nessuna pubblicazione prima della conferma", published == 0 && manager.IsClockPulsePending);
            Rx(Frame("!REGBOUND", end));
            Check("conferma pubblica un solo dump e sblocca i comandi", confirmed == 1 && published == 1 &&
                vm.AReg == 0x55 && vm.PcReg == 0x1234 && !manager.IsClockPulsePending);
            dump[0] = 0x77;
            foreach (string invalid in new[] { "clock iniziale", "durata", "PC", "corto", "base64", "dump mancante", "dump duplicato", "marker legacy", "clock inatteso" })
            {
                Begin();
                if (invalid != "dump mancante") Rx(Frame("!BDUMP", dump));
                byte[] bad = (byte[])end.Clone();
                switch (invalid)
                {
                    case "clock iniziale": bad[5] = 41; bad[9] = 135; break;
                    case "durata": bad[9] = 135; break;
                    case "PC": bad[1]++; break;
                    case "corto": bad = new byte[9]; break;
                }
                if (invalid == "dump duplicato") Rx(Frame("!BDUMP", dump));
                else if (invalid == "marker legacy") Rx("!PULSEEND;");
                else if (invalid == "clock inatteso") Rx(Frame("!PULSE", new byte[9]));
                else Rx(invalid == "base64" ? "!REGBOUNDinvalid!;" : Frame("!REGBOUND", bad));
                Rx(Frame("!REGBOUND", end));
                Check("rifiuta " + invalid, !manager.IsClockPulsePending && confirmed == 1 && published == 1 && vm.AReg == 0x55);
            }
            Begin(); manager.CancelReadForReset();
            Rx(Frame("!BDUMP", dump)); Rx(Frame("!REGBOUND", end));
            Check("reset ignora risposte tardive", confirmed == 1 && published == 1 && !manager.IsClockPulsePending);
            Begin(); typeof(SerialManager).GetMethod("CompleteClockPulse", flags)!.Invoke(manager, new object[] { "Timeout di prova" });
            Rx(Frame("!BDUMP", dump)); Rx(Frame("!REGBOUND", end));
            Check("timeout ignora risposte tardive", confirmed == 1 && published == 1 && !manager.IsClockPulsePending);
            Begin(); Set("boundaryRegisterStart", 0xffffffe0u);
            byte[] rollover = { 0x12, 0x34, 0xff, 0xff, 0xff, 0xe0, 0, 0, 0, 0x3e };
            Rx(Frame("!BDUMP", dump)); Rx(Frame("!REGBOUND", rollover));
            Check("contatori confermati anche al rollover", confirmed == 2 && published == 2 && vm.AReg == 0x77);
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,SMEM:A,ECLK:0,CLKPULSE:ON,REGSNAP:ON;");
            Check("firmware precedente non abilita la nuova lettura", !vm.HasRegisterBoundarySnapshotSupport);
        }
        finally
        {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.CancelReadForReset(); manager.SerialPort.Dispose();
        }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
