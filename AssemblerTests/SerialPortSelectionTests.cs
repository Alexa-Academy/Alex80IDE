using System.Reflection;
using System.Text.Json;
using Alex80_IDE;
using Alex80_IDE.Models;

internal static class SerialPortSelectionTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} porte ricordate: {name}"); if (!ok) failures++; }

        var legacy = JsonSerializer.Deserialize<UserSettings>("{}")!;
        Check("impostazioni precedenti senza cronologia", legacy.RecentSerialPorts.Count == 0 && legacy.SerialPortBaudRates.Count == 0);
        int saves = 0;
        var settings = new UserSettings { RecentSerialPorts = new() { "COM8", "COM5" } };
        var state = new SerialPortSelectionState(settings.RecentSerialPorts, settings.SerialPortBaudRates, (ports, baudRates) =>
        { settings.RecentSerialPorts = ports.ToList(); settings.SerialPortBaudRates = baudRates.ToDictionary(pair => pair.Key, pair => pair.Value); saves++; });
        Check("avvio sceglie la porta nota più recente disponibile", state.ChooseIndex(new[] { "COM1", "COM5", "COM8" }, null) == 2);
        Check("assenza dell'ultima porta usa una precedente conosciuta", state.ChooseIndex(new[] { "COM1", "COM5" }, null) == 1);
        Check("assenza di porte rimuove la selezione", state.ChooseIndex(Array.Empty<string>(), null) == -1);
        Check("senza porte note conserva il comportamento iniziale", state.ChooseIndex(new[] { "COM1" }, null) == 0);
        Check("ricomparsa della scheda nota supera il ripiego automatico", state.ChooseIndex(new[] { "COM1", "COM8" }, null) == 1);
        state.SelectManually("COM1");
        Check("scelta manuale prevale sulla cronologia", state.ChooseIndex(new[] { "COM8", "COM1", "COM5" }, null) == 1);
        Check("porta aperta prevale durante la connessione", state.ChooseIndex(new[] { "COM8", "COM1", "COM5" }, "COM5") == 2);
        Check("scansioni e scelte manuali non riscrivono la cronologia", saves == 0);
        state.RememberConnectedPort("COM5", 9600);
        Check("connessione identificata diventa la più recente", saves == 1 && settings.RecentSerialPorts.SequenceEqual(new[] { "COM5", "COM8" }));
        state.RememberConnectedPort("COM5", 9600);
        Check("INFO ripetuti non risalvano la stessa preferenza", saves == 1);
        Check("connessione salva il baud rate insieme alla porta", settings.SerialPortBaudRates["COM5"] == 9600);
        state.RememberConnectedPort("COM5", 115200);
        Check("nuovo baud sulla stessa porta aggiorna la preferenza", saves == 2 && settings.SerialPortBaudRates["COM5"] == 115200 &&
            settings.RecentSerialPorts.SequenceEqual(new[] { "COM5", "COM8" }));
        state.RememberConnectedPort("COM8", 9600);
        Check("porte diverse mantengono velocità indipendenti", settings.SerialPortBaudRates["COM5"] == 115200 && settings.SerialPortBaudRates["COM8"] == 9600);
        state.RememberConnectedPort("COM5", 115200);
        var reloaded = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        var restarted = new SerialPortSelectionState(reloaded.RecentSerialPorts, reloaded.SerialPortBaudRates, (_, _) => { });
        Check("cronologia sopravvive al riavvio", restarted.ChooseIndex(new[] { "COM8", "COM5" }, null) == 1);
        Check("baud rate per porta sopravvive al riavvio", restarted.GetRememberedBaudRate("COM5") == 115200 &&
            restarted.GetRememberedBaudRate("COM8") == 9600);
        Check("porte senza baud memorizzato non inventano una velocità", restarted.GetRememberedBaudRate("COM9") is null);
        Check("nome porta diverso non è scambiato per un'identità nota", restarted.ChooseIndex(new[] { "COM1", "COM9" }, null) == 0);
        for (int i = 0; i < 12; i++) state.RememberConnectedPort($"port-{i}", 115200);
        Check("cronologia limitata alle ultime dieci porte", settings.RecentSerialPorts.Count == 10 &&
            settings.RecentSerialPorts[0] == "port-11" && settings.RecentSerialPorts[^1] == "port-2");
        Check("rimozione dalla cronologia elimina anche il baud", settings.SerialPortBaudRates.Count == 10 && !settings.SerialPortBaudRates.ContainsKey("COM5"));
        var sanitized = new SerialPortSelectionState(new[] { "", " ", "COM5", "COM5" }, null, (_, _) => { });
        Check("cronologia tollera campi vuoti e duplicati", sanitized.ChooseIndex(new[] { "COM1", "COM5" }, null) == 1);

        Check("cronologia precedente senza baud resta compatibile", sanitized.GetRememberedBaudRate("COM5") is null);

        // Actual view-model refresh path: collection resets must not turn an
        // automatic index into a manual preference, nor open a serial port.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var vm = new MainViewModel(new NoDialogs());
        var timer = (System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!;
        timer.Stop();
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        // Inject in-memory preferences so this test never writes the user's settings.
        var vmSelection = new SerialPortSelectionState(new[] { "test-port-b", "test-port-a" },
            new Dictionary<string, int> { ["test-port-a"] = 9600, ["test-port-b"] = 115200 }, (_, _) => { });
        typeof(MainViewModel).GetField("_serialPortSelection", flags)!.SetValue(vm, vmSelection);
        var refresh = typeof(MainViewModel).GetMethod("ApplyAvailableSerialPorts", flags)!;
        void Refresh(string[] ports) => refresh.Invoke(vm, new object[] { ports });
        try
        {
            Refresh(new[] { "test-port-a", "test-port-b" });
            Check("preselezione ripristina il baud della porta", vm.SelectedIdx == 1 && vm.SelectedBaudRate == 115200);
            vm.SelectedIdx = 0;
            Check("scelta manuale ripristina il baud dell'altra porta", vm.SelectedBaudRate == 9600);
            vm.SelectedIdx = 1;
            Check("ritorno alla prima porta ripristina la sua velocità", vm.SelectedBaudRate == 115200);
            // Simulate a pending manual baud edit without persisting global settings.
            typeof(MainViewModel).GetField("_selectedBaudRate", flags)!.SetValue(vm, 9600);
            Refresh(new[] { "test-port-b", "test-port-a", "test-port-c" });
            Check("refresh della ComboBox mantiene la porta scelta dopo un riordino", vm.SelectedIdx == 0 && vm.PortsList[0] == "test-port-b");
            Check("scansione e riordino non cancellano un baud appena modificato", vm.SelectedBaudRate == 9600);
            Refresh(Array.Empty<string>());
            Check("refresh senza dispositivi svuota la tendina", vm.PortsList.Count == 0 && vm.SelectedIdx == -1);
            Refresh(new[] { "test-port-a", "test-port-b" });
            Check("reintroduzione delle porte ripristina la scelta manuale", vm.SelectedIdx == 1);
            Check("ricomparsa della porta ripristina il baud riuscito", vm.SelectedBaudRate == 115200);
            Check("preselezione non apre la seriale né dichiara una scheda connessa", !manager.SerialPort.IsOpen && !vm.IsSerialOpened && !vm.IsBoardConnected);
        }
        finally { timer.Dispose(); manager.SerialPort.Dispose(); }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
