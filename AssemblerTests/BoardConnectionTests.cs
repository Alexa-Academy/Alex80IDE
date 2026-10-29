using System.Reflection;
using Avalonia.Threading;
using Alex80_IDE;

// Exercise the actual INFO parser -> SerialManager event -> UI properties path
// without opening a physical port or changing hardware configuration.
internal static class BoardConnectionTests
{
    public static int Run()
    {
        var failures = 0;
        void Check(string name, bool passed)
        {
            Console.WriteLine($"{(passed ? "ok  " : "FAIL")} {name}");
            if (!passed) failures++;
        }

        var legacy = CardConfig.Parse("CARD:ALEX80,SCLK:A,FCLK:50,ECLK:1,SMEM:E");
        Check("INFO legacy Z80: campi opzionali assenti", legacy.LogEnabled is null &&
            legacy.DebugEnabled is null && legacy.MonitorEnabled is null && legacy.FullLogEnabled is null && legacy.ClockActive);
        var optional = CardConfig.Parse("CARD:ALEX80micro,SCLK:E,FCLK:500,ECLK:0,SMEM:A,LOG:on,DEBUG:1,MONITOR:0,FUTURE:abc");
        Check("INFO flag ON/OFF e numerici", optional.LogEnabled == true &&
            optional.DebugEnabled == true && optional.MonitorEnabled == false && !optional.IsClockFromArduino);
        var malformed = CardConfig.Parse("CARD:ALEX80micro,SCLK:,SMEM:,FCLK:,LOG:?,DEBUG:,MONITOR:");
        Check("INFO incompleto non interrompe la ricezione", malformed.LogEnabled is null &&
            malformed.DebugEnabled is null && malformed.MonitorEnabled is null);
        Check("INFO versione firmware opzionale", legacy.FirmwareVersion == "" &&
            CardConfig.Parse("CARD:ALEX80µ,FWVER: 1.0.0 ,FUTURE:abc").FirmwareVersion == "1.0.0");

        var vm = new MainViewModel(new NoDialogs());
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        var receive = typeof(SerialManager).GetMethod("HandleSerialMessage", flags)!;
        void Receive(string payload)
        {
            receive.Invoke(manager, new object[] { "!INFO:" + payload + ";" });
            Dispatcher.UIThread.RunJobs();
        }
        try
        {
            vm.IsSerialOpened = true;
            var originalOutput = Console.Out;
            var receivedOutput = new StringWriter();
            try
            {
                Console.SetOut(receivedOutput);
                Receive("CARD:ALEX80µ,SCLK:A,FCLK:32768,ECLK:1,SMEM:A,LOG:ON,LOGMODE:FULL,DEBUG:ON,MONITOR:OFF");
            }
            finally { Console.SetOut(originalOutput); }
            Check("connessione: stato reale completo applicato alla UI", vm.IsBoardConnected &&
                vm.IsClockFromArduino && vm.IsMemoryFromArduino && vm.ClockFrequency == "32768" &&
                vm.IsClockActive && vm.LogEnabled && vm.DebugEnabled && vm.MonitorEnabled == false &&
                vm.FullLogEnabled && vm.CanConfigureLogMode);
            Check("configurazione ricevuta non invia comandi alla scheda",
                !receivedOutput.ToString().Contains("Porta seriale non aperta."));
            Check("etichette log/debug aggiornate", vm.BusLogToggleText == "Disabilita log continuo" && vm.CanDumpRegisters);
            Check("micro senza versione: requisito visibile senza bloccare la scheda",
                vm.IsFirmwareVersionUnknown && vm.HasFirmwareWarning && !vm.IsFirmwareUpdateRequired &&
                vm.CardConnectionText.Contains("versione da verificare") &&
                vm.CardConnectionToolTip.Contains(MainViewModel.RequiredMicroFirmwareVersion));

            var firmwareNotifications = new HashSet<string>();
            vm.PropertyChanged += (_, e) => firmwareNotifications.Add(e.PropertyName ?? "");
            Receive("CARD:ALEX80µ,FWVER:1.0.0");
            Check("firmware corrente: versione mostrata e requisito soddisfatto",
                vm.FirmwareVersion == "1.0.0" && vm.CardConnectionText == "ALEX80µ · firmware 1.0.0" &&
                !vm.HasFirmwareWarning && vm.RequiredFirmwareVersion == "1.0.0" &&
                vm.CardConnectionToolTip.Contains("soddisfatto"));
            Check("notifiche aggiornano testo, tooltip e avviso nella barra di stato",
                firmwareNotifications.Contains(nameof(MainViewModel.CardConnectionText)) &&
                firmwareNotifications.Contains(nameof(MainViewModel.CardConnectionToolTip)) &&
                firmwareNotifications.Contains(nameof(MainViewModel.HasFirmwareWarning)));
            Receive("CARD:ALEX80µ,FWVER:0.9.0");
            Check("firmware precedente: aggiornamento richiesto", vm.IsFirmwareUpdateRequired &&
                vm.HasFirmwareWarning && vm.CardConnectionText.Contains("aggiornamento richiesto"));
            Receive("CARD:ALEX80µ,FWVER:1.10.0");
            Check("versioni confrontate numericamente", !vm.HasFirmwareWarning);
            Receive("CARD:ALEX80µ,FWVER:1.0");
            Check("versione numerica senza patch equivale a patch zero", !vm.HasFirmwareWarning);
            Receive("CARD:ALEX80µ,FWVER:invalid");
            Check("versione malformata: segnalata senza interrompere INFO", vm.IsBoardConnected &&
                vm.IsFirmwareVersionUnknown && !vm.IsFirmwareUpdateRequired);
            Receive("CARD:ALEX80µ,FWVER:1.0.0");

            // A subsequent report also corrects optimistic state after a refused command.
            vm.LogEnabled = false; vm.DebugEnabled = false;
            Receive("CARD:ALEX80µ,SCLK:A,FCLK:32768,ECLK:1,SMEM:A,LOG:ON,DEBUG:ON,MONITOR:ON,FWVER:1.0.0");
            Check("INFO corregge lo stato dopo un comando rifiutato", vm.LogEnabled && vm.DebugEnabled &&
                vm.MonitorEnabled == true && vm.MonitorStatusText.Contains("attivo"));

            vm.IsSerialOpened = false;
            Check("disconnessione: nessuno stato resta dalla sessione precedente", !vm.IsBoardConnected &&
                !vm.LogEnabled && !vm.DebugEnabled && !vm.IsClockActive && vm.MonitorEnabled is null &&
                !vm.FullLogEnabled && !vm.CanConfigureLogMode);
            Check("disconnessione: versione e avvisi azzerati", vm.FirmwareVersion == "" &&
                !vm.HasFirmwareWarning && vm.CardConnectionText == "scheda non connessa");
            vm.IsSerialOpened = true;
            Receive("CARD:ALEX80µ,SCLK:E,FCLK:100,ECLK:0,SMEM:E,LOG:OFF,LOGMODE:COMPACT,DEBUG:OFF,MONITOR:OFF");
            Check("riconnessione: nuovi valori OFF e sorgenti esterne", vm.IsBoardConnected &&
                !vm.LogEnabled && !vm.DebugEnabled && !vm.IsClockActive && !vm.IsClockFromArduino &&
                !vm.IsMemoryFromArduino && vm.ClockFrequency == "100" && !vm.FullLogEnabled && vm.CanConfigureLogMode);
            Check("INFO legacy non eredita la versione precedente", vm.FirmwareVersion == "" && vm.IsFirmwareVersionUnknown);

            Receive("CARD:ALEX80,FWVER:0.1.0");
            Check("ALEX80 modulare: nessun requisito micro applicato", !vm.HasFirmwareWarning &&
                vm.RequiredFirmwareVersion == "" && vm.CardConnectionText == "ALEX80 · firmware 0.1.0");

            Receive("CARD:eZ80Test,SCLK:E,FCLK:0,ECLK:1,SMEM:E");
            Check("INFO eZ80 esistente resta compatibile", vm.IsEz80 && vm.IsBoardConnected &&
                !vm.CanUseDebug && !vm.CanUseBusLog && vm.MonitorEnabled is null && !vm.CanConfigureLogMode);
            Check("eZ80: versione e requisito micro non ereditati", vm.FirmwareVersion == "" &&
                !vm.HasFirmwareWarning && vm.RequiredFirmwareVersion == "" && vm.CardConnectionText == "eZ80Test");

            // Full-log metadata must not feed debugger/reset opcodes into the
            // program disassembler or move its pending mnemonic to another row.
            manager.RestartBoard(); vm.LogTable.Clear();
            void Log(ushort address, byte data, byte control)
            {
                var bytes = new byte[] { (byte)(address >> 8), (byte)address, data, control };
                receive.Invoke(manager, new object[] { "!LOG" + Convert.ToBase64String(bytes) + ";" });
                Dispatcher.UIThread.RunJobs();
            }
            Log(0x2000, 0x3E, 0x1A); // LD A,n
            Log(0x2001, 0xC5, 0x5A); // injected PUSH BC, excluded from disassembly
            Log(0xFFFF, 0x00, 0xBF); // reset/floating bus
            Log(0x2001, 0x42, 0x1B); // real immediate operand
            Check("log completo: righe interne identificate", vm.LogTable.Count == 4 &&
                vm.LogTable[1].IsInjected && vm.LogTable[1].TracePhase == "Lettura registri" &&
                vm.LogTable[2].IsReset && vm.LogTable[2].TracePhase == "Reset");
            Check("log completo: disassemblaggio del programma preservato",
                vm.LogTable[0].Mnemonic.Contains("42") && vm.LogTable[1].Mnemonic == "" &&
                vm.LogTable[2].Mnemonic == "");
        }
        finally
        {
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.SerialPort.Dispose();
        }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    {
        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;
    }
}
