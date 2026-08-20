using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Alex80_IDE.Helpers;

/// <summary>
/// Scorciatoie da tastiera delle finestre che ospitano l'editor.
/// Il tasto modificatore è quello di sistema: Command su macOS, Ctrl su Windows e Linux.
/// </summary>
public static class AppShortcuts
{
    /// <summary>Nome del modificatore, per i suggerimenti mostrati all'utente.</summary>
    public static string CommandModifierName => CommandModifier == KeyModifiers.Meta ? "Cmd" : "Ctrl";

    private static KeyModifiers CommandModifier =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Aggiunge le scorciatoie alla finestra.</summary>
    public static void Install(Window window)
    {
        var command = CommandModifier;

        Bind(window, Key.N, command, vm => vm.NewTabCommand);
        Bind(window, Key.O, command, vm => vm.OpenFileCommand);
        Bind(window, Key.S, command, vm => vm.SaveFileCommand);
        Bind(window, Key.S, command | KeyModifiers.Shift, vm => vm.SaveFileAsCommand);
        Bind(window, Key.B, command, vm => vm.AssembleCommand);
        Bind(window, Key.W, command, vm => vm.CloseTabCommand, vm => vm.SelectedDocument);
    }

    private static void Bind(
        Window window,
        Key key,
        KeyModifiers modifiers,
        Func<MainViewModel, ICommand?> command,
        Func<MainViewModel, object?>? parameter = null)
    {
        window.KeyBindings.Add(new KeyBinding
        {
            Gesture = new KeyGesture(key, modifiers),
            Command = new ViewModelCommand(window, command, parameter)
        });
    }

    /// <summary>
    /// Inoltra la scorciatoia al comando del view model, ripescandolo dal DataContext della
    /// finestra al momento della pressione: così funziona anche se il DataContext viene
    /// assegnato dopo la costruzione della finestra, o sostituito in seguito.
    /// </summary>
    private sealed class ViewModelCommand : ICommand
    {
        private readonly Window _window;
        private readonly Func<MainViewModel, ICommand?> _command;
        private readonly Func<MainViewModel, object?>? _parameter;

        public ViewModelCommand(Window window, Func<MainViewModel, ICommand?> command, Func<MainViewModel, object?>? parameter)
        {
            _window = window;
            _command = command;
            _parameter = parameter;
        }

        public bool CanExecute(object? parameter) =>
            Resolve() is { } target && target.CanExecute(ResolveParameter());

        public void Execute(object? parameter)
        {
            var target = Resolve();
            var argument = ResolveParameter();

            if (target?.CanExecute(argument) == true)
            {
                target.Execute(argument);
            }
        }

        // CanExecute viene interrogato a ogni pressione del tasto, quindi non serve
        // propagare le notifiche di cambiamento dei comandi sottostanti.
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        private ICommand? Resolve() => _window.DataContext is MainViewModel viewModel ? _command(viewModel) : null;

        private object? ResolveParameter() =>
            _window.DataContext is MainViewModel viewModel ? _parameter?.Invoke(viewModel) : null;
    }
}
