using System.Reflection;
using System.Text.Json;
using Alex80_IDE;
using Avalonia.Threading;

internal static class RegisterRefreshTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} {name}"); if (!ok) failures++; }
        Check("registri automatici: default e salvataggio JSON", JsonSerializer.Deserialize<UserSettings>("{}")!.AutoRefreshRegisters &&
            !JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(new UserSettings { AutoRefreshRegisters = false }))!.AutoRefreshRegisters);
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
        void Dump(byte a, ushort pc = 0x2000)
        {
            byte[] bytes = { a, 1, 2, 3, 4, 5, 6, 0x12, 0x34, 0x56, 0x78, 0x80, 0, 0x41, (byte)(pc >> 8), (byte)pc };
            Rx("!DUMP" + Convert.ToBase64String(bytes) + ";");
        }
        try
        {
            Vm("_autoRefreshRegisters", true); // Do not write the user's settings in tests.
            vm.IsSerialOpened = true;
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,FCLK:50,ECLK:1,SMEM:A,DEBUG:ON,REGUPDATE:ON,CLKPULSE:ON;");
            Check("capability registri riconosciuta", vm.HasRegisterUpdateSupport);
            Serial("registerRequestPending", true);
            Check("attesa registri blocca step, memoria e dump", !vm.StepCommand.CanExecute(null) &&
                !vm.ReadMemoryCommand.CanExecute(null) && !vm.DumpCommand.CanExecute(null));
            Dump(0x28);
            Check("dump automatico aggiorna registri e flag e conclude lo step", vm.AReg == 0x28 && vm.FReg == 0x41 &&
                vm.IxReg == 0x1234 && !manager.IsRegisterRequestPending && vm.RegisterRefreshStatus.Contains("aggiornati"));
            Vm("_autoRefreshRegisters", false);
            vm.StepCommand.Execute(null);
            Dump(0x29, 0x2001);
            Check("flag spento conserva la lettura precedente", vm.AReg == 0x28 && vm.PcReg == 0x2000 &&
                vm.RegisterRefreshStatus.Contains("da aggiornare") && vm.InstructionPrediction == "");
            Vm("_manualRegisterRefreshRequested", true);
            Serial("registerRequestPending", true);
            Dump(0x2A);
            Check("rilettura manuale funziona anche con flag spento", vm.AReg == 0x2A && !manager.IsRegisterRequestPending);
            Dump(0x2B);
            Check("autorizzazione rilettura manuale consumata una sola volta", vm.AReg == 0x2A);
            Serial("registerRequestPending", true); Rx("!STEP;");
            Check("step senza iniezione termina senza cambiare registri", !manager.IsRegisterRequestPending && vm.AReg == 0x2A);

            Vm("_autoRefreshRegisters", true);
            Rx("!INFO:CARD:ALEX80µ,SCLK:A,FCLK:50,ECLK:0,SMEM:A,DEBUG:OFF,REGUPDATE:ON,CLKPULSE:ON;");
            Serial("clockPulsePending", true); Serial("clockPulseRegisters", true);
            byte[] sample = { 0x20, 0, 0x3C, 0x1A, 5, 0, 0, 0, 20 };
            Rx("!PULSE" + Convert.ToBase64String(sample) + ";");
            int rows = vm.LogTable.Count;
            Check("clock con lettura resta pendente dopo il campione", manager.IsClockPulsePending && !vm.PulseClockCommand.CanExecute(null));
            Dump(0x30);
            Check("clock con lettura attende il fermo anche dopo il dump", manager.IsClockPulsePending && vm.AReg == 0x30);
            Rx("!PULSEEND;");
            Check("conferma fermo sblocca il clock senza duplicare log", !manager.IsClockPulsePending &&
                vm.PulseClockCommand.CanExecute(null) && vm.LogTable.Count == rows);
            Serial("clockPulsePending", true); Serial("clockPulseRegisters", true); Rx("!PULSEEND;");
            Check("impulso senza confine conclude senza inventare registri", !manager.IsClockPulsePending && vm.AReg == 0x30);
            Serial("clockPulsePending", true); Serial("clockPulseRegisters", false);
            Rx("!PULSE" + Convert.ToBase64String(sample) + ";");
            Check("impulso legacy si conclude al singolo campione", !manager.IsClockPulsePending);
            Serial("registerRequestPending", true); Rx("!DUMPbad!;");
            Check("dump malformato sblocca la UI e segnala l'errore", !manager.IsRegisterRequestPending && vm.RegisterRefreshStatus.Contains("non valida"));
            Serial("registerRequestPending", true); Rx("!DUMPAA==;");
            Check("dump corto non aggiorna i valori", !manager.IsRegisterRequestPending && vm.AReg == 0x30);
            Serial("registerRequestPending", true); Rx("!ERROR:CPU HALTED, RESET REQUIRED;");
            Check("HALT termina l'attesa dei registri", !manager.IsRegisterRequestPending && vm.RegisterRefreshStatus.Contains("HALTED"));

            Vm("_autoRefreshRegisters", false);
            Rx("!INFO:CARD:eZ80Test,SCLK:E,FCLK:0,ECLK:1,SMEM:E;");
            byte[] wide = new byte[30]; wide[0] = 0x55; wide[3] = 0x90; wide[24] = 0xFF; wide[25] = 0xF0;
            Rx("!DUMP24" + Convert.ToBase64String(wide) + ";");
            Check("eZ80 con flag spento aggiorna lo stato di arresto", vm.IsCpuHalted && vm.AReg == 0x30 && !vm.HasRegisterUpdateSupport);
            Vm("_manualRegisterRefreshRequested", true);
            Rx("!DUMP24" + Convert.ToBase64String(wide) + ";");
            Check("rilettura manuale eZ80 conserva i 24 bit", vm.AReg == 0x55 && vm.Pc24 == 0xFFF000);
            Serial("registerRequestPending", true); vm.ResetCommand.Execute(null); Dispatcher.UIThread.RunJobs();
            Check("reset cancella la richiesta registri pendente", !manager.IsRegisterRequestPending);
            vm.IsSerialOpened = false;
            Check("disconnessione azzera capability e stato registri", !vm.HasRegisterUpdateSupport && vm.RegisterRefreshStatus.Contains("Nessuna lettura"));
        }
        finally
        {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.SerialPort.Dispose();
        }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
