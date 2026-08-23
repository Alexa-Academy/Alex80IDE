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

    /// <summary>
    /// Scorciatoia con il modificatore della piattaforma. La usa anche il menu, così le
    /// voci mostrano lo stesso tasto che la finestra ascolta davvero.
    /// </summary>
    public static KeyGesture Gesture(Key key, KeyModifiers extraModifiers = KeyModifiers.None) =>
        new(key, CommandModifier | extraModifiers);

    /// <summary>
    /// Comando che inoltra a quello del view model, ripescandolo dal DataContext al momento
    /// dell'uso. Serve a chi si aggancia alla finestra prima che il view model esista:
    /// le scorciatoie e le voci di menu.
    /// </summary>
    public static ICommand ViewModelCommand(
        Window window,
        Func<MainViewModel, ICommand?> command,
        Func<MainViewModel, object?>? parameter = null) =>
        new ViewModelCommandProxy(window, command, parameter);

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
            Command = new ViewModelCommandProxy(window, command, parameter)
        });
    }

    /// <summary>
    /// Inoltra al comando del view model, ripescandolo dal DataContext della finestra al
    /// momento dell'uso: così funziona anche se il DataContext viene assegnato dopo la
    /// costruzione della finestra, o sostituito in seguito.
    /// <para>
    /// Le notifiche di cambiamento vanno propagate davvero: una voce di menu legge lo stato
    /// una volta sola, quando le viene assegnato il comando. A quel punto il view model non
    /// esiste ancora, e senza propagazione la voce resterebbe spenta per sempre.
    /// </para>
    /// </summary>
    private sealed class ViewModelCommandProxy : ICommand
    {
        private readonly Window _window;
        private readonly Func<MainViewModel, ICommand?> _command;
        private readonly Func<MainViewModel, object?>? _parameter;
        private ICommand? _target;

        public ViewModelCommandProxy(Window window, Func<MainViewModel, ICommand?> command, Func<MainViewModel, object?>? parameter)
        {
            _window = window;
            _command = command;
            _parameter = parameter;

            _window.DataContextChanged += (_, _) => Rebind();
            Rebind();
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) =>
            _target is { } target && target.CanExecute(ResolveParameter());

        public void Execute(object? parameter)
        {
            var argument = ResolveParameter();

            if (_target?.CanExecute(argument) == true)
            {
                _target.Execute(argument);
            }
        }

        /// <summary>Si aggancia al comando del view model corrente e ne segue lo stato.</summary>
        private void Rebind()
        {
            if (_target is not null)
            {
                _target.CanExecuteChanged -= OnTargetCanExecuteChanged;
            }

            _target = _window.DataContext is MainViewModel viewModel ? _command(viewModel) : null;

            if (_target is not null)
            {
                _target.CanExecuteChanged += OnTargetCanExecuteChanged;
            }

            OnTargetCanExecuteChanged(this, EventArgs.Empty);
        }

        private void OnTargetCanExecuteChanged(object? sender, EventArgs e) =>
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        private object? ResolveParameter() =>
            _window.DataContext is MainViewModel viewModel ? _parameter?.Invoke(viewModel) : null;
    }
}
