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
        Check("impostazioni precedenti senza cronologia", legacy.RecentSerialPorts.Count == 0);
        int saves = 0;
        var settings = new UserSettings { RecentSerialPorts = new() { "COM8", "COM5" } };
        var state = new SerialPortSelectionState(settings.RecentSerialPorts, ports =>
        { settings.RecentSerialPorts = ports.ToList(); saves++; });
        Check("avvio sceglie la porta nota più recente disponibile", state.ChooseIndex(new[] { "COM1", "COM5", "COM8" }, null) == 2);
        Check("assenza dell'ultima porta usa una precedente conosciuta", state.ChooseIndex(new[] { "COM1", "COM5" }, null) == 1);
        Check("assenza di porte rimuove la selezione", state.ChooseIndex(Array.Empty<string>(), null) == -1);
        Check("senza porte note conserva il comportamento iniziale", state.ChooseIndex(new[] { "COM1" }, null) == 0);
        Check("ricomparsa della scheda nota supera il ripiego automatico", state.ChooseIndex(new[] { "COM1", "COM8" }, null) == 1);
        state.SelectManually("COM1");
        Check("scelta manuale prevale sulla cronologia", state.ChooseIndex(new[] { "COM8", "COM1", "COM5" }, null) == 1);
        Check("porta aperta prevale durante la connessione", state.ChooseIndex(new[] { "COM8", "COM1", "COM5" }, "COM5") == 2);
        Check("scansioni e scelte manuali non riscrivono la cronologia", saves == 0);
        state.RememberConnectedPort("COM5");
        Check("connessione identificata diventa la più recente", saves == 1 && settings.RecentSerialPorts.SequenceEqual(new[] { "COM5", "COM8" }));
        state.RememberConnectedPort("COM5");
        Check("INFO ripetuti non risalvano la stessa preferenza", saves == 1);
        var reloaded = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        var restarted = new SerialPortSelectionState(reloaded.RecentSerialPorts, _ => { });
        Check("cronologia sopravvive al riavvio", restarted.ChooseIndex(new[] { "COM8", "COM5" }, null) == 1);
        Check("nome porta diverso non è scambiato per un'identità nota", restarted.ChooseIndex(new[] { "COM1", "COM9" }, null) == 0);
        for (int i = 0; i < 12; i++) state.RememberConnectedPort($"port-{i}");
        Check("cronologia limitata alle ultime dieci porte", settings.RecentSerialPorts.Count == 10 &&
            settings.RecentSerialPorts[0] == "port-11" && settings.RecentSerialPorts[^1] == "port-2");
        var sanitized = new SerialPortSelectionState(new[] { "", " ", "COM5", "COM5" }, _ => { });
        Check("cronologia tollera campi vuoti e duplicati", sanitized.ChooseIndex(new[] { "COM1", "COM5" }, null) == 1);

        // Actual view-model refresh path: collection resets must not turn an
        // automatic index into a manual preference, nor open a serial port.
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var vm = new MainViewModel(new NoDialogs());
        var timer = (System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!;
        timer.Stop();
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        var refresh = typeof(MainViewModel).GetMethod("ApplyAvailableSerialPorts", flags)!;
        void Refresh(string[] ports) => refresh.Invoke(vm, new object[] { ports });
        try
        {
            Refresh(new[] { "test-port-a", "test-port-b" });
            vm.SelectedIdx = 1;
            Refresh(new[] { "test-port-b", "test-port-a", "test-port-c" });
            Check("refresh della ComboBox mantiene la porta scelta dopo un riordino", vm.SelectedIdx == 0 && vm.PortsList[0] == "test-port-b");
            Refresh(Array.Empty<string>());
            Check("refresh senza dispositivi svuota la tendina", vm.PortsList.Count == 0 && vm.SelectedIdx == -1);
            Refresh(new[] { "test-port-a", "test-port-b" });
            Check("reintroduzione delle porte ripristina la scelta manuale", vm.SelectedIdx == 1);
            Check("preselezione non apre la seriale né dichiara una scheda connessa", !manager.SerialPort.IsOpen && !vm.IsSerialOpened && !vm.IsBoardConnected);
        }
        finally { timer.Dispose(); manager.SerialPort.Dispose(); }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
