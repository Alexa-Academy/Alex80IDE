using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace Alex80_IDE.Helpers;

/// <summary>
/// Menu dell'applicazione. È lo stesso menu su tutte le piattaforme, presentato secondo
/// le convenzioni di ciascuna: su macOS Avalonia lo esporta nella barra di sistema, su
/// Windows e Linux compare nella <c>NativeMenuBar</c> in cima alla finestra.
/// <para>
/// Viene costruito nel codice e non in XAML perché le scorciatoie devono usare il
/// modificatore della piattaforma (vedi <see cref="AppShortcuts"/>): scritte in XAML
/// sarebbero fissate a un tasto solo.
/// </para>
/// </summary>
public static class AppMenu
{
    /// <summary>Costruisce il menu e lo aggancia alla finestra.</summary>
    public static void Install(Window window)
    {
        var menu = new NativeMenu();

        menu.Items.Add(new NativeMenuItem("File") { Menu = BuildFileMenu(window) });
        menu.Items.Add(new NativeMenuItem("Impostazioni") { Menu = BuildSettingsMenu() });

        NativeMenu.SetMenu(window, menu);
    }

    /// <summary>
    /// Apertura e salvataggio dei sorgenti e del progetto. Stanno qui e non su una
    /// barra di pulsanti: sono comandi che si usano di rado e che occupavano spazio
    /// accanto ai comandi di assemblaggio, che invece si usano di continuo.
    /// </summary>
    private static NativeMenu BuildFileMenu(Window window)
    {
        var menu = new NativeMenu();

        menu.Items.Add(Item(window, "Nuovo sorgente", vm => vm.NewTabCommand, Key.N));
        menu.Items.Add(Item(window, "Apri sorgente...", vm => vm.OpenFileCommand, Key.O));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(window, "Salva", vm => vm.SaveFileCommand, Key.S));
        menu.Items.Add(Item(window, "Salva con nome...", vm => vm.SaveFileAsCommand, Key.S, KeyModifiers.Shift));
        menu.Items.Add(Item(window, "Salva array Arduino...", vm => vm.SaveArduinoArrayCommand));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(window, "Nuovo progetto...", vm => vm.NewProjectCommand));
        menu.Items.Add(Item(window, "Apri progetto...", vm => vm.OpenProjectCommand));
        menu.Items.Add(Item(window, "Aggiungi file al progetto...", vm => vm.AddFilesToProjectCommand));
        menu.Items.Add(Item(window, "Chiudi progetto", vm => vm.CloseProjectCommand));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(Item(window, "Chiudi scheda", vm => vm.CloseTabCommand, Key.W, parameter: vm => vm.SelectedDocument));

        return menu;
    }

    /// <summary>Tema dell'interfaccia. I due elementi restano allineati al tema in uso.</summary>
    private static NativeMenu BuildSettingsMenu()
    {
        var dark = ThemeItem("Scuro", AppTheme.Dark);
        var light = ThemeItem("Chiaro", AppTheme.Light);

        var themeMenu = new NativeMenu();
        themeMenu.Items.Add(dark);
        themeMenu.Items.Add(light);

        void Sync(AppTheme theme)
        {
            dark.IsChecked = theme == AppTheme.Dark;
            light.IsChecked = theme == AppTheme.Light;
        }

        Sync(AppThemeManager.CurrentTheme);
        AppThemeManager.ThemeChanged += Sync;

        var menu = new NativeMenu();
        menu.Items.Add(new NativeMenuItem("Tema") { Menu = themeMenu });

        return menu;
    }

    private static NativeMenuItem ThemeItem(string header, AppTheme theme)
    {
        var item = new NativeMenuItem(header)
        {
            ToggleType = NativeMenuItemToggleType.Radio
        };

        item.Click += (_, _) => AppThemeManager.SetTheme(theme);

        return item;
    }

    /// <summary>
    /// Una voce che inoltra a un comando del view model. Il comando viene ripescato dal
    /// DataContext al momento del clic: il menu nasce con la finestra, prima che il view
    /// model sia assegnato.
    /// </summary>
    private static NativeMenuItem Item(
        Window window,
        string header,
        Func<MainViewModel, ICommand?> command,
        Key? shortcut = null,
        KeyModifiers extraModifiers = KeyModifiers.None,
        Func<MainViewModel, object?>? parameter = null)
    {
        var item = new NativeMenuItem(header)
        {
            Command = AppShortcuts.ViewModelCommand(window, command, parameter)
        };

        if (shortcut is { } key)
        {
            item.Gesture = AppShortcuts.Gesture(key, extraModifiers);
        }

        return item;
    }
}
