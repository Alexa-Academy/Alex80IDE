using System.Reflection;
using System.Text.Json;
using Alex80_IDE;
using Alex80_IDE.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

internal static class BusLogScrollTests
{
    public static int Run()
    {
        int failures = 0;
        void Check(string name, bool ok)
        { Console.WriteLine($"{(ok ? "ok  " : "FAIL")} scorrimento log: {name}"); if (!ok) failures++; }

        Check("impostazioni precedenti mantengono lo scorrimento attivo",
            JsonSerializer.Deserialize<UserSettings>("{}")!.BusLogAutoScrollEnabled);
        var settings = new UserSettings(); int saves = 0;
        var state = new BusLogScrollState(settings.BusLogAutoScrollEnabled, enabled =>
        { settings.BusLogAutoScrollEnabled = enabled; saves++; });
        Check("pausa automatica cambia solo la sessione", state.Suspend() && !state.IsEnabled &&
            settings.BusLogAutoScrollEnabled && saves == 0);
        state.Suspend();
        Check("pause ripetute non salvano", saves == 0);
        // Simulate saving an unrelated setting, then reopening the application.
        settings.MainWindowWidth = 1400;
        var reopened = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        Check("salvataggio finestra e riavvio non conservano la pausa automatica",
            new BusLogScrollState(reopened.BusLogAutoScrollEnabled, _ => { }).IsEnabled);
        state.SetManually(true); state.SetManually(false);
        reopened = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        Check("disabilitazione manuale si conserva al riavvio", !reopened.BusLogAutoScrollEnabled && saves == 2);
        Check("pausa con preferenza spenta non la riattiva", !state.Suspend() && !settings.BusLogAutoScrollEnabled && saves == 2);
        state.SetManually(true);
        reopened = JsonSerializer.Deserialize<UserSettings>(JsonSerializer.Serialize(settings))!;
        Check("riabilitazione manuale si conserva al riavvio", reopened.BusLogAutoScrollEnabled && saves == 3);

        var vm = new MainViewModel(new NoDialogs());
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var manager = (SerialManager)typeof(MainViewModel).GetField("_serialManager", flags)!.GetValue(vm)!;
        var behavior = new AutoScrollBehavior();
        try
        {
            Check("IDE ripristina la preferenza salvata", vm.IsAutoScrollEnabled == UserSettings.Load().BusLogAutoScrollEnabled);
            // Replace only this preference's saver; never write user settings.
            saves = 0; settings.BusLogAutoScrollEnabled = true;
            typeof(MainViewModel).GetField("_busLogScroll", flags)!.SetValue(vm,
                new BusLogScrollState(true, enabled => { settings.BusLogAutoScrollEnabled = enabled; saves++; }));
            var grid = new DataGrid();
            var toggle = new ToggleSwitch();
            toggle.Bind(ToggleSwitch.IsCheckedProperty, new Binding(nameof(MainViewModel.IsAutoScrollEnabled))
                { Source = vm, Mode = BindingMode.TwoWay });
            behavior.Bind(AutoScrollBehavior.IsAutoScrollEnabledProperty,
                new Binding(nameof(MainViewModel.IsAutoScrollEnabled)) { Source = vm });
            behavior.SuspendAutoScrollCommand = vm.SuspendBusLogAutoScrollCommand;
            behavior.Attach(grid);
            // Observe what the behavior leaves to the normal key handler. An
            // unhosted DataGrid cannot run its platform-specific shortcuts.
            bool keyWasHandled = false;
            grid.AddHandler(InputElement.KeyDownEvent, (_, e) =>
            { keyWasHandled = e.Handled; e.Handled = true; }, RoutingStrategies.Tunnel);
            Dispatcher.UIThread.RunJobs();

            void Key(Key key) => grid.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
            Key(Avalonia.Input.Key.C);
            Check("tasti estranei allo scorrimento non sospendono", vm.IsAutoScrollEnabled && toggle.IsChecked == true);
            Key(Avalonia.Input.Key.PageUp);
            Check("PageUp spegne subito lo scorrimento senza salvare", !vm.IsAutoScrollEnabled &&
                !behavior.IsAutoScrollEnabled && !keyWasHandled && settings.BusLogAutoScrollEnabled && saves == 0);
            Dispatcher.UIThread.RunJobs();
            Check("interruttore spento senza memorizzare il ritorno del binding", toggle.IsChecked == false && saves == 0);
            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();
            Check("interruttore riabilita e salva", vm.IsAutoScrollEnabled && behavior.IsAutoScrollEnabled && saves == 1);

            var wheel = new PointerWheelEventArgs(grid, new Avalonia.Input.Pointer(1, PointerType.Mouse, true), grid,
                default, 0, new PointerPointProperties(), KeyModifiers.None, new Vector(0, 1));
            grid.RaiseEvent(wheel);
            Check("rotella sospende e lascia proseguire l'evento", !vm.IsAutoScrollEnabled &&
                !wheel.Handled && settings.BusLogAutoScrollEnabled && saves == 1);
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();
            toggle.IsChecked = false; Dispatcher.UIThread.RunJobs();
            Check("spegnimento esplicito dopo una pausa viene salvato", !vm.IsAutoScrollEnabled &&
                !settings.BusLogAutoScrollEnabled && saves == 3);

            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();
            int before = saves;
            var gesture = new ScrollGestureEventArgs(1, new Vector(0, 1));
            grid.RaiseEvent(gesture);
            Check("gesto di scorrimento sospende senza salvarsi o consumare l'evento",
                !vm.IsAutoScrollEnabled && !gesture.Handled && saves == before);
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();

            var bar = new ScrollBar { Maximum = 100 };
            void ApplyBar(ScrollBar scrollBar)
            {
                var scope = new NameScope(); scope.Register("PART_VerticalScrollbar", scrollBar);
                typeof(AutoScrollBehavior).GetMethod("OnTemplateApplied", flags)!.Invoke(behavior,
                    new object[] { grid, new TemplateAppliedEventArgs(scope) });
            }
            void Press(ScrollBar scrollBar) => scrollBar.RaiseEvent(new PointerPressedEventArgs(
                scrollBar, new Avalonia.Input.Pointer(2, PointerType.Mouse, true), scrollBar,
                default, 0, new PointerPointProperties(), KeyModifiers.None, 1));
            ApplyBar(bar);
            bar.Value = 40;
            Check("cambiamenti programmatici della barra non sospendono", vm.IsAutoScrollEnabled);
            before = saves;
            Press(bar);
            Check("pressione sulla barra sospende senza salvare", !vm.IsAutoScrollEnabled && saves == before);
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();
            var replacement = new ScrollBar(); ApplyBar(replacement);
            Press(bar);
            Check("cambio template sgancia la vecchia barra", vm.IsAutoScrollEnabled);
            before = saves;
            Press(replacement);
            Check("barra del nuovo template ancora funzionante", !vm.IsAutoScrollEnabled && saves == before);
            Dispatcher.UIThread.RunJobs();
            toggle.IsChecked = true; Dispatcher.UIThread.RunJobs();
            behavior.Detach();
            Key(Avalonia.Input.Key.Up);
            Press(replacement);
            Check("sgancio del pannello rimuove i gestori", vm.IsAutoScrollEnabled);
        }
        finally
        {
            behavior.Detach();
            ((System.Timers.Timer)typeof(MainViewModel).GetField("_serialPortCheckTimer", flags)!.GetValue(vm)!).Dispose();
            manager.SerialPort.Dispose();
        }
        return failures;
    }

    private sealed class NoDialogs : IDialogService
    { public Task ShowErrorAsync(string title, string message) => Task.CompletedTask; }
}
