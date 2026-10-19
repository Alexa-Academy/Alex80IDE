using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Alex80_IDE;
using Alex80_IDE.Helpers;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Konamiman.Nestor80.Assembler;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;
using Alex80_IDE.Models;
using Alex80_IDE.ViewModels;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Timer = System.Timers.Timer;

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly object _fileWatcherLock = new();
    private readonly Dictionary<DocumentViewModel, FileSystemWatcher> _fileWatchers = new();
    private readonly Dictionary<DocumentViewModel, CancellationTokenSource> _reloadDebounceTokens = new();
    private readonly Dictionary<string, DocumentViewModel> _listingDocuments = new();
    
    private const int DefaultClockFreq = 50;
    private const int DefaultStartAdd = 0x0000;
    private const int DefaultBytesRead = 80;
    
    public ObservableCollection<DocumentViewModel> OpenDocuments { get; } = new();
    private DocumentViewModel _selectedDocument;
    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveFileAsCommand { get; }
    public DocumentViewModel SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            _selectedDocument = value;
            OnPropertyChanged();
            (SaveFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveFileAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AssembleCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand NewTabCommand { get; }
    public ICommand AssembleCommand { get; }
    public ICommand SaveArduinoArrayCommand { get; }

    /// <summary>"Cmd" su macOS, "Ctrl" altrove: serve a scrivere le scorciatoie nei tooltip.</summary>
    public string CommandModifierName => AppShortcuts.CommandModifierName;
    
    [ObservableProperty]
    private string _cardName = string.Empty;
    public string CardBadgeText => string.IsNullOrWhiteSpace(CardName) ? "scheda non connessa" : CardName;

    /// <summary>
    /// Vero quando una scheda ha risposto e ha detto chi e'. La porta aperta da sola non
    /// basta: finche' non arriva !INFO non si sa se dall'altra parte c'e' una scheda
    /// dell'ecosistema ALEX80 o un dispositivo qualunque, e i comandi partirebbero alla
    /// cieca. E' la condizione di tutti i comandi che presuppongono una scheda.
    /// <para>
    /// Non serve controllare anche la porta: chiudendola il nome della scheda viene
    /// azzerato insieme al resto della sua identita'.
    /// </para>
    /// </summary>
    public bool IsBoardConnected => !string.IsNullOrWhiteSpace(CardName);

    /// <summary>
    /// Porta aperta ma nessuna scheda che si sia ancora presentata. E' uno stato di
    /// passaggio che dura qualche secondo, e va distinto dalla porta chiusa: li' c'e'
    /// qualcosa da fare, qui c'e' solo da aspettare.
    /// </summary>
    public bool IsWaitingForBoard => IsSerialOpened && !IsBoardConnected;

    partial void OnCardNameChanged(string value)
    {
        OnPropertyChanged(nameof(CardBadgeText));
        OnPropertyChanged(nameof(IsBoardConnected));
        OnPropertyChanged(nameof(IsWaitingForBoard));
        ReadMemoryCommand.NotifyCanExecuteChanged();
        WriteMemoryCommand.NotifyCanExecuteChanged();
        NotifyCpuContextChanged();
    }
    
    [ObservableProperty]
    private string _assembledOutput;
    
    [ObservableProperty]
    private string _lineByLineOutput;

    [ObservableProperty]
    private bool _isAssemblerOutputVisible = true;

    /// <summary>Esito dell'ultima assemblata, mostrato nella status bar.</summary>
    [ObservableProperty]
    private string _assemblerStatus = "nessuna assemblata";
    
    public RelayCommand ToggleAssemblerOutputCommand { get; }
    
    public ObservableCollection<RowElement> DataTable {  get; set; }
    public ObservableCollection<LogElement> LogTable { get; set; }
    
    [ObservableProperty]
    private string _serialMessageText = string.Empty;
    
    private void AppendSerialLine(string line)
    {
        SerialMessageText += line + Environment.NewLine;
    }
    public ObservableCollection<string> PortsList { get; set; }
    
    [ObservableProperty]
    private int _selectedIdx = -1;
    
    [ObservableProperty]
    private string _toggleButtonContent = "Apri";
    
    private readonly SerialManager _serialManager;
    
    private readonly Timer _serialPortCheckTimer;

    [ObservableProperty] 
    private bool _isSerialOpened;

    partial void OnIsSerialOpenedChanged(bool value)
    {
        if (!value)
        {
            // Con la porta se ne va anche l'identita' della scheda: tenerla vorrebbe dire
            // continuare a decidere in base a un hardware che non c'e' piu'.
            CardName = string.Empty;
            IsEz80 = false;
            IsAlex80Full = false;
            IsAlex80Micro = false;
        }

        // Aprendo o chiudendo la porta il confronto riparte da zero: i registri di
        // prima appartengono a una sessione che non c'e' piu'.
        ResetRegisterComparison();

        ReadMemoryCommand.NotifyCanExecuteChanged();
        WriteMemoryCommand.NotifyCanExecuteChanged();

        // Aprire o chiudere la porta cambia chi decide fra scheda e CPU di progetto.
        NotifyCpuContextChanged();
    }
    
    [ObservableProperty] 
    private bool _isAutoScrollEnabled = true;
    
    [ObservableProperty] 
    private int _progressValue;
    
    [ObservableProperty] 
    private bool _progressVisible;

    private byte[]? _fileBytesToWrite;
    
    [ObservableProperty]
    private string _addressHex = DefaultStartAdd.ToString("X4");
    
    [ObservableProperty] 
    private string _numBytes = DefaultBytesRead.ToString();
    
    [ObservableProperty] 
    private bool _isClockFromArduino;
    
    [ObservableProperty] 
    private bool _isClockActive;

    partial void OnIsClockActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(ClockToggleText));
        NotifyDumpAvailabilityChanged();
    }

    /// <summary>
    /// La lettura dei registri dipende da parecchie cose insieme - scheda, debug, clock,
    /// da dove arriva la memoria - e ognuna cambia per conto suo: la notifica sta in un
    /// posto solo perche' non se ne dimentichi una.
    /// </summary>
    private void NotifyDumpAvailabilityChanged()
    {
        OnPropertyChanged(nameof(CanDumpRegisters));
        OnPropertyChanged(nameof(DumpUnavailableText));
        DumpCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Etichetta del comando del log: dice cosa fa il clic.</summary>
    public string BusLogToggleText => LogEnabled ? "Disabilita log" : "Abilita log";

    partial void OnLogEnabledChanged(bool value) => OnPropertyChanged(nameof(BusLogToggleText));
    
    [ObservableProperty] 
    private bool _isMemoryFromArduino;

    private bool _isApplyingCardConfig;
    
    [ObservableProperty]
    private byte _aReg;
    public string ARegHex => AReg.ToString("X2");
    partial void OnARegChanged(byte value)
    {
        OnPropertyChanged(nameof(ARegHex));
    }
    
    [ObservableProperty]
    private byte _bReg;
    public string BRegHex => BReg.ToString("X2");
    partial void OnBRegChanged(byte value)
    {
        OnPropertyChanged(nameof(BRegHex));
    }
    
    [ObservableProperty]
    private byte _cReg;
    public string CRegHex => CReg.ToString("X2");
    partial void OnCRegChanged(byte value)
    {
        OnPropertyChanged(nameof(CRegHex));
    }
    
    [ObservableProperty]
    private byte _dReg;
    public string DRegHex => DReg.ToString("X2");
    partial void OnDRegChanged(byte value)
    {
        OnPropertyChanged(nameof(DRegHex));
    }
    
    [ObservableProperty]
    private byte _eReg;
    public string ERegHex => EReg.ToString("X2");
    partial void OnERegChanged(byte value)
    {
        OnPropertyChanged(nameof(ERegHex));
    }
    
    [ObservableProperty]
    private byte _hReg;
    public string HRegHex => HReg.ToString("X2");
    partial void OnHRegChanged(byte value)
    {
        OnPropertyChanged(nameof(HRegHex));
    }
    
    [ObservableProperty]
    private byte _lReg;
    public string LRegHex => LReg.ToString("X2");
    partial void OnLRegChanged(byte value)
    {
        OnPropertyChanged(nameof(LRegHex));
    }
    
    // Su eZ80 i registri indice, lo stack pointer e il PC sono a 24 bit: le
    // proprieta' a 16 bit restano per lo Z80, e i valori estesi arrivano dal
    // !DUMP24. Le stringhe .Hex scelgono la larghezza in base a IsEz80, cosi'
    // i binding XAML esistenti continuano a valere senza modifiche.
    [ObservableProperty]
    private ushort _ixReg;
    
    public string IxRegHex => UseWideAddresses ? Ix24.ToString("X6") : IxReg.ToString("X4");
    partial void OnIxRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(IxRegHex));
    }
    
    [ObservableProperty]
    private ushort _iyReg;
    
    public string IyRegHex => UseWideAddresses ? Iy24.ToString("X6") : IyReg.ToString("X4");
    partial void OnIyRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(IyRegHex));
    }
    
    [ObservableProperty]
    private ushort _spReg;
    
    public string SpRegHex => UseWideAddresses ? Spl24.ToString("X6") : SpReg.ToString("X4");
    partial void OnSpRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(SpRegHex));
    }
    
    [ObservableProperty]
    private ushort _pcReg;
    
    public string PcRegHex => UseWideAddresses ? Pc24.ToString("X6") : PcReg.ToString("X4");
    partial void OnPcRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(PcRegHex));
    }

    // ---- stato esteso eZ80, popolato da !DUMP24 ----

    [ObservableProperty] private bool _isEz80;

    /// <summary>
    /// Su scheda eZ80Test permette di lavorare come se fosse uno Z80: indirizzi
    /// e registri a 16 bit. Serve quando il programma sul bersaglio gira in
    /// modo Z80 anziche' ADL, dove i 24 bit non hanno significato.
    /// </summary>
    /// <summary>
    /// Acceso di partenza: senza una scheda collegata il bersaglio del progetto e' quasi
    /// sempre uno Z80, e i registri vanno mostrati a 16 bit. Collegando un eZ80 lo si
    /// spegne dal pannello Scheda per tornare agli indirizzi a 24 bit.
    /// </summary>
    [ObservableProperty] private bool _z80CompatibilityMode = true;

    /// <summary>Tipo di scheda collegata, usato per decidere cosa fa "play".</summary>
    [ObservableProperty] private bool _isAlex80Full;
    [ObservableProperty] private bool _isAlex80Micro;

    /// <summary>
    /// "Play" fa cose diverse a seconda della scheda, ma il significato per chi
    /// lo preme e' lo stesso: manda in esecuzione il programma appena caricato.
    /// </summary>
    public string RunToolTip => IsEz80Context
        ? "Carica il PC con l'indirizzo indicato e avvia la CPU"
        : "Invia NMI: fa saltare lo Z80 al vettore di avvio";

    public bool CanRun => IsBoardConnected;

    [RelayCommand]
    private void Run()
    {
        if (IsEz80Context)
        {
            // Stesso campo indirizzo usato da lettura e scrittura, e dopo
            // un'assemblata contiene gia' l'indirizzo di partenza del progetto.
            var address = ParseAddressField(DefaultStartAdd);
            _serialManager.RunFrom(address);
            IsCpuHalted = false;
            AppendSerialLine($"Avvio da {address:X6}h");
        }
        else
        {
            // Anche l'ALEX80 micro: il suo firmware implementa "nmi" e pulsa la linea
            // Z80_NMI, quindi l'esclusione che c'era qui era rimasta indietro.
            SendNmi();
        }
    }

    /// <summary>Vero quando la CPU e' ferma in break ZDI (bit 7 di STAT).</summary>
    [ObservableProperty] private bool _isCpuHalted;

    /// <summary>
    /// Icona del comando: dice cosa fa il clic, non lo stato della CPU. Con la CPU che
    /// gira il pulsante ferma, quindi mostra la pausa; ferma, riprende, quindi mostra
    /// barra e triangolo.
    /// </summary>
    public string BreakButtonIconKey => IsCpuHalted ? "ContinueIconData" : "PauseIconData";

    public string BreakButtonToolTip => IsCpuHalted
        ? "Riprende dal punto in cui la CPU e' ferma, senza toccare il PC (cont)"
        : "Ferma la CPU (break ZDI)";

    partial void OnIsCpuHaltedChanged(bool value)
    {
        OnPropertyChanged(nameof(BreakButtonIconKey));
        OnPropertyChanged(nameof(BreakButtonToolTip));
    }

    /// <summary>
    /// Ferma o fa ripartire la CPU. Serve perche' ogni accesso a memoria via
    /// ZDI richiede il core fermo, e averlo sotto controllo esplicito rende
    /// evidente in che stato si sta leggendo.
    /// </summary>
    [RelayCommand]
    private void ToggleBreak()
    {
        if (IsCpuHalted)
        {
            _serialManager.ContinueCpu();
            IsCpuHalted = false;
        }
        else
        {
            _serialManager.BreakCpu();
            IsCpuHalted = true;
        }
    }

    /// <summary>
    /// Vero quando la CPU di progetto e' l'eZ80. Serve perche' assemblare non
    /// richiede una scheda collegata: senza questo, dopo una compilazione gli
    /// indirizzi verrebbero mostrati a 16 bit finche' non si apre la seriale.
    /// </summary>
    private bool IsEz80Cpu => string.Equals(CpuName, CardConfig.Ez80CardName, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(CpuName, "EZ80", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Vero quando l'eZ80 e' in gioco. Governa i comandi specifici dell'eZ80 e la
    /// larghezza con cui si mostrano indirizzi e registri.
    /// <para>
    /// Con una scheda collegata decide la scheda, non la CPU di progetto: e'
    /// l'hardware davanti a noi a dire cosa si puo' fare. Senza questa precedenza
    /// un progetto impostato su eZ80 con una ALEX80 collegata faceva mandare a
    /// "play" un comando ZDI invece dell'NMI, comparire il pulsante di break e
    /// mostrare gli indirizzi a 24 bit su una macchina che ne ha 16.
    /// </para>
    /// <para>
    /// A porta chiusa decide invece la CPU di progetto: assemblare non richiede una
    /// scheda, e dopo una compilazione per eZ80 gli indirizzi vanno mostrati per
    /// quello che sono.
    /// </para>
    /// </summary>
    public bool IsEz80Context => IsBoardConnected ? IsEz80 : IsEz80Cpu;

    /// <summary>
    /// Vero quando indirizzi e registri vanno trattati a 24 bit: l'eZ80 e' in gioco
    /// e non si sta lavorando in modo Z80.
    /// </summary>
    public bool UseWideAddresses => IsEz80Context && !Z80CompatibilityMode;

    /// <summary>
    /// Clock e cicli di memoria serviti dall'Arduino esistono solo sulle schede
    /// ALEX80: sull'eZ80 il collegamento e' ZDI e questi segnali non ci sono, cosi'
    /// come non ha senso imporre una frequenza di clock. Il giudizio guarda alla
    /// scheda collegata e non alla CPU di progetto: e' l'hardware davanti a noi a
    /// dire quali segnali esistono.
    /// </summary>
    public bool CanConfigureArduinoSignals => IsBoardConnected && !IsEz80;

    /// <summary>
    /// Scegliere fra modo ADL e modo Z80 ha senso solo sull'eZ80. Su una ALEX80 la
    /// CPU e' uno Z80 e non c'e' niente da scegliere: la levetta resta spenta.
    /// </summary>
    public bool CanChooseCpuMode => IsBoardConnected && IsEz80;

    /// <summary>
    /// Il clock si comanda solo quando a generarlo e' l'Arduino: con l'oscillatore della
    /// scheda non c'e' niente da avviare o fermare, e sull'eZ80 il clock non passa di qui
    /// (il firmware ZDI risponde "the eZ80 clocks itself"). Vale anche per la frequenza,
    /// che senza il clock dell'Arduino non ha nulla da regolare.
    /// </summary>
    public bool CanControlClock => CanConfigureArduinoSignals && IsClockFromArduino;

    /// <summary>Etichetta del comando: dice cosa fa il clic, non lo stato attuale.</summary>
    public string ClockToggleText => IsClockActive ? "Ferma" : "Avvia";

    /// <summary>Vero quando la scheda collegata e' una ALEX80, modulare o micro.</summary>
    public bool IsAlex80Board => IsAlex80Full || IsAlex80Micro;

    /// <summary>
    /// Vero quando la scheda puo' davvero restituire i registri.
    /// <para>
    /// Sull'eZ80 sempre: il firmware ferma la CPU da se', legge, e la fa ripartire se
    /// stava girando, quindi funziona anche a programma in corsa.
    /// </para>
    /// <para>
    /// Sull'ALEX80 micro serve la via dell'iniezione, che il firmware concede solo con
    /// memoria servita dall'Arduino, debug attivo e clock che gira: sono le stesse
    /// condizioni che controlla lui prima di provarci.
    /// </para>
    /// <para>
    /// Sull'ALEX80 modulare mai: li' il dump passa dal modo monitor, che carica in RAM
    /// un programma suo e si prende il clock. Accenderlo per leggere i registri
    /// vorrebbe dire cancellare il programma dell'utente.
    /// </para>
    /// </summary>
    public bool CanDumpRegisters =>
        IsBoardConnected &&
        (IsEz80 ||
         (IsAlex80Micro && IsMemoryFromArduino && DebugEnabled && (!IsClockFromArduino || IsClockActive)));

    /// <summary>
    /// Il debug passo-passo esiste solo sulle ALEX80: sull'eZ80 il firmware ZDI accetta
    /// "debug on" e non fa niente (alex_ok e basta), perche' li' l'esecuzione si governa
    /// con break e cont. Tenerlo acceso voleva dire un pulsante che cambiava etichetta
    /// senza che sulla scheda cambiasse nulla.
    /// </summary>
    public bool CanUseDebug => IsBoardConnected && IsAlex80Board;

    /// <summary>
    /// Registrare il log vuole una scheda che sappia mostrare il bus. Sull'eZ80 si passa
    /// da ZDI e il firmware risponde "no bus visibility over ZDI": non c'e' niente da
    /// registrare.
    /// </summary>
    public bool CanUseBusLog => IsBoardConnected && IsAlex80Board;

    /// <summary>
    /// Lo strumento resta raggiungibile anche a scheda scollegata: le righe gia'
    /// catturate si rileggono lo stesso, ed e' proprio dopo aver staccato la scheda che
    /// capita di volerle guardare. Sparisce solo quando c'e' collegato un eZ80, che un
    /// log del bus non lo produrra' mai.
    /// </summary>
    private bool IsBusLogToolAvailable => !(IsBoardConnected && IsEz80);

    /// <summary>Perche' la lettura non e' disponibile, quando non lo e'.</summary>
    public string DumpUnavailableText
    {
        get
        {
            if (CanDumpRegisters)
            {
                return string.Empty;
            }

            if (!IsBoardConnected)
            {
                return "Collega una scheda per leggere i registri.";
            }

            if (IsAlex80Full)
            {
                return "Su ALEX80 la lettura dei registri passa dal modo monitor, che carica in RAM un programma suo: cancellerebbe il tuo. Non è disponibile da qui.";
            }

            return "Servono la memoria servita dall'Arduino, il debug attivo e il clock in moto.";
        }
    }

    /// <summary>
    /// La CPU di destinazione si sceglie finche' non c'e' una ALEX80 collegata: quella
    /// scheda monta uno Z80 e non c'e' altro da scegliere. Il blocco impedisce di
    /// impostare una combinazione impossibile, ma non riscrive il progetto: un .a80
    /// gia' impostato su un'altra CPU resta com'e', e lo dice <see cref="CpuMismatchText"/>.
    /// </summary>
    public bool CanChooseCpu => !(IsBoardConnected && IsAlex80Board);

    /// <summary>
    /// Vero quando il progetto ha per bersaglio l'eZ80 ma la scheda collegata e' una
    /// ALEX80: si puo' assemblare lo stesso, ma il binario non gira su quella scheda.
    /// </summary>
    public bool HasCpuMismatch => IsBoardConnected && IsAlex80Board && IsEz80Cpu;

    /// <summary>
    /// La CPU per cui si assembla davvero. Con una ALEX80 collegata e' Z80 comunque:
    /// quella scheda monta uno Z80 e un binario per eZ80 non ci girerebbe. Il file di
    /// progetto non viene riscritto, la scelta resta quella che c'e' scritta dentro, ma
    /// finche' la scheda e' collegata vale questa - ed e' questa che mostrano la status
    /// bar e l'esito dell'assemblata, perche' un indicatore che dicesse la CPU scelta
    /// mentre se ne usa un'altra sarebbe peggio che non averlo.
    /// </summary>
    public string EffectiveCpuName => IsBoardConnected && IsAlex80Board ? "Z80" : CpuName;

    /// <summary>
    /// Quel che la status bar scrive della CPU. Bersaglio dell'assemblata e modo di
    /// indirizzamento sono due cose diverse e possono non coincidere: un eZ80 che lavora
    /// in modo Z80 assembla per eZ80 ma indirizza 64 KB via MBASE. Scritto il solo
    /// bersaglio, "CPU: EZ80" accanto a registri a 16 bit sembrava un errore.
    /// </summary>
    public string CpuStatusText =>
        IsEz80Context && Z80CompatibilityMode
            ? $"CPU: {EffectiveCpuName} · modo Z80"
            : $"CPU: {EffectiveCpuName}";

    /// <summary>Il perche' del disaccordo, per il pannello e per l'esito dell'assemblata.</summary>
    public string CpuMismatchText =>
        HasCpuMismatch
            ? $"Il progetto è impostato su {CpuName}, ma la scheda collegata è {CardName}: finché resta collegata si assembla per Z80. Il progetto non viene modificato."
            : string.Empty;

    /// <summary>
    /// Interpreta il campo indirizzo come esadecimale, che e' il formato in cui
    /// l'IDE lo scrive (vedi AddressHex = ToString("X4")). Ritorna il valore di
    /// ripiego se il testo non e' valido o eccede l'ampiezza corrente: senza il
    /// ramo OverflowException un indirizzo come FFF000 faceva terminare
    /// l'applicazione invece di essere respinto.
    /// </summary>
    private uint ParseAddressField(uint fallback)
    {
        try
        {
            var parsed = uint.Parse(AddressHex, System.Globalization.NumberStyles.HexNumber);
            if (!UseWideAddresses && parsed > 0xFFFF) return fallback;
            return parsed & 0xFFFFFF;
        }
        catch (FormatException) { return fallback; }
        catch (OverflowException) { return fallback; }
    }
    [ObservableProperty] private uint _bc24;
    [ObservableProperty] private uint _de24;
    [ObservableProperty] private uint _hl24;
    [ObservableProperty] private uint _ix24;
    [ObservableProperty] private uint _iy24;
    [ObservableProperty] private uint _spl24;
    [ObservableProperty] private uint _pc24;
    [ObservableProperty] private ushort _sps16;
    [ObservableProperty] private byte _mbReg;
    [ObservableProperty] private byte _statReg;
    [ObservableProperty] private byte _iReg;
    [ObservableProperty] private byte _rReg;
    [ObservableProperty] private bool _iff2;
    [ObservableProperty] private bool _isAdl;

    public string Bc24Hex => Bc24.ToString("X6");
    public string De24Hex => De24.ToString("X6");
    public string Hl24Hex => Hl24.ToString("X6");
    public string Spl24Hex => Spl24.ToString("X6");
    public string Sps16Hex => Sps16.ToString("X4");
    public string MbRegHex => MbReg.ToString("X2");
    public string StatRegHex => StatReg.ToString("X2");
    public string IRegHex => IReg.ToString("X2");
    public string RRegHex => RReg.ToString("X2");
    public string AdlText => IsAdl ? "ADL" : "Z80";
    public string Iff2Text => Iff2 ? "1" : "0";

    // Scheda collegata e CPU di progetto decidono insieme cosa si puo' fare e con che
    // larghezza si leggono gli indirizzi: quando cambia uno dei due vanno notificate
    // tutte le proprieta' che ne derivano, non solo quelle appena riscritte.
    private void NotifyCpuContextChanged()
    {
        OnPropertyChanged(nameof(UseWideAddresses));
        OnPropertyChanged(nameof(IsBoardConnected));
        OnPropertyChanged(nameof(IsWaitingForBoard));
        OnPropertyChanged(nameof(IsEz80Context));
        OnPropertyChanged(nameof(RunToolTip));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(CanConfigureArduinoSignals));
        OnPropertyChanged(nameof(CanChooseCpuMode));
        OnPropertyChanged(nameof(CanControlClock));
        StartStopBoardCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsAlex80Board));
        OnPropertyChanged(nameof(CanChooseCpu));
        OnPropertyChanged(nameof(HasCpuMismatch));
        OnPropertyChanged(nameof(CpuMismatchText));
        OnPropertyChanged(nameof(EffectiveCpuName));
        OnPropertyChanged(nameof(CpuStatusText));
        OnPropertyChanged(nameof(CanUseDebug));
        OnPropertyChanged(nameof(CanUseBusLog));
        DebugCommand.NotifyCanExecuteChanged();
        EnableDisableLogsCommand.NotifyCanExecuteChanged();
        BusLogTool.IsAvailable = IsBusLogToolAvailable;
        NotifyDumpAvailabilityChanged();
        OnPropertyChanged(nameof(IxRegHex));
        OnPropertyChanged(nameof(IyRegHex));
        OnPropertyChanged(nameof(SpRegHex));
        OnPropertyChanged(nameof(PcRegHex));

        // Qui a cambiare e' il numero di cifre con cui si scrive un indirizzo, non
        // il valore letto dalla scheda: le celle si riscrivono senza evidenziare.
        RefreshRegisterCells(highlight: false);
    }

    partial void OnIsEz80Changed(bool value) => NotifyCpuContextChanged();

    // Quale scheda risponde lo dice il messaggio !INFO, che arriva dopo l'apertura
    // della porta: senza queste due il pannello resterebbe fermo allo stato di prima.
    partial void OnIsAlex80FullChanged(bool value) => NotifyCpuContextChanged();

    partial void OnIsAlex80MicroChanged(bool value) => NotifyCpuContextChanged();

    partial void OnZ80CompatibilityModeChanged(bool value)
    {
        NotifyCpuContextChanged();

        // Non e' solo una questione di come si mostrano gli indirizzi: decide
        // anche con quale modalita' il firmware raggiunge la memoria, e in modo
        // Z80 la RAM a FFF000h non e' raggiungibile affatto.
        if (IsSerialOpened)
        {
            _serialManager.SetAdlMode(!value);
        }
    }

    partial void OnBc24Changed(uint value) => OnPropertyChanged(nameof(Bc24Hex));
    partial void OnDe24Changed(uint value) => OnPropertyChanged(nameof(De24Hex));
    partial void OnHl24Changed(uint value) => OnPropertyChanged(nameof(Hl24Hex));
    partial void OnIx24Changed(uint value) => OnPropertyChanged(nameof(IxRegHex));
    partial void OnIy24Changed(uint value) => OnPropertyChanged(nameof(IyRegHex));
    partial void OnSpl24Changed(uint value)
    {
        OnPropertyChanged(nameof(Spl24Hex));
        OnPropertyChanged(nameof(SpRegHex));
    }
    partial void OnPc24Changed(uint value) => OnPropertyChanged(nameof(PcRegHex));
    partial void OnSps16Changed(ushort value) => OnPropertyChanged(nameof(Sps16Hex));
    partial void OnMbRegChanged(byte value) => OnPropertyChanged(nameof(MbRegHex));
    partial void OnStatRegChanged(byte value) => OnPropertyChanged(nameof(StatRegHex));
    partial void OnIRegChanged(byte value) => OnPropertyChanged(nameof(IRegHex));
    partial void OnRRegChanged(byte value) => OnPropertyChanged(nameof(RRegHex));
    partial void OnIsAdlChanged(bool value) => OnPropertyChanged(nameof(AdlText));
    partial void OnIff2Changed(bool value) => OnPropertyChanged(nameof(Iff2Text));
    
    [ObservableProperty]
    private byte _fReg;

    /// <summary>
    /// I flag del registro F, uno per elemento: la vista li mette su una riga
    /// dedicata, dove ognuno ha spazio per la sigla, lo stato e il suggerimento
    /// che ne spiega il significato. L'ordine e' quello dei bit, dal 7 allo 0.
    /// </summary>
    public IReadOnlyList<CpuFlagViewModel> Flags { get; } =
    [
        new("S", 0x80,
            "Segno: acceso quando il bit 7 del risultato vale 1, cioe' risultato negativo in complemento a due"),
        new("Z", 0x40,
            "Zero: acceso quando il risultato e' zero"),
        new("H", 0x10,
            "Riporto intermedio, dal bit 3 al bit 4. Lo usa DAA per correggere le operazioni in BCD"),
        new("P/V", 0x04,
            "Parita' o traboccamento: parita' pari nelle operazioni logiche, traboccamento in quelle aritmetiche con segno"),
        new("N", 0x02,
            "Acceso dopo una sottrazione, spento dopo un'addizione. Lo usa DAA per sapere in che verso correggere"),
        new("C", 0x01,
            "Riporto oltre il bit 7, o prestito in una sottrazione. Lo leggono anche rotazioni e scorrimenti")
    ];

    public string FlagsString => GetZ80FlagsString(FReg);
    
    // I flag li rilegge RefreshRegisterCells insieme ai registri, non questa: e' li'
    // che si sa se il valore e' diverso da quello del passo precedente. Qui resta la
    // sola stringa riassuntiva, che non ha bisogno del confronto.
    partial void OnFRegChanged(byte value) => OnPropertyChanged(nameof(FlagsString));

    // ---- registri e flag della vista, con l'evidenza di quelli appena cambiati ----

    /// <summary>Per quanto resta acceso l'evidenziatore su un registro o un flag appena cambiato.</summary>
    private static readonly TimeSpan RegisterHighlightDuration = TimeSpan.FromSeconds(2);

    /// <summary>Spegne l'evidenza da solo: il debug e' a passi, non a comandi di annullamento.</summary>
    private DispatcherTimer? _registerHighlightTimer;

    /// <summary>
    /// Alla prima rilettura non c'e' un valore di prima con cui confrontarsi: senza
    /// questo ogni registro risulterebbe cambiato appena la scheda risponde.
    /// </summary>
    private bool _hasRegisterSnapshot;

    /// <summary>Registri della vista Z80, nell'ordine in cui la vista li dispone.</summary>
    public IReadOnlyList<CpuRegisterViewModel> Z80Registers { get; private set; } = [];

    /// <summary>Registri della vista eZ80: le coppie a 24 bit e quelli che il formato Z80 non rappresenta.</summary>
    public IReadOnlyList<CpuRegisterViewModel> Ez80Registers { get; private set; } = [];

    /// <summary>
    /// Costruisce le due viste dei registri. Leggono le stesse proprieta' .Hex di
    /// prima, cosi' la larghezza degli indirizzi continua a dipendere da un posto
    /// solo; quello che aggiungono e' il confronto con la lettura precedente.
    /// </summary>
    private void InitializeRegisterCells()
    {
        Z80Registers =
        [
            new("A", "Accumulatore: ci finisce il risultato della maggior parte delle operazioni aritmetiche e logiche", () => ARegHex),
            new("B", "Registro generale. Fa da contatore in DJNZ e nelle istruzioni di blocco", () => BRegHex),
            new("C", "Registro generale. Porta il numero di porta nelle IN e OUT indirizzate da C", () => CRegHex),
            new("D", "Registro generale, meta' alta della coppia DE", () => DRegHex),
            new("E", "Registro generale, meta' bassa della coppia DE", () => ERegHex),
            new("H", "Registro generale, meta' alta della coppia HL, l'indirizzo negli accessi indiretti a memoria", () => HRegHex),
            new("L", "Registro generale, meta' bassa della coppia HL", () => LRegHex),
            new("IX", "Registro indice: indirizzo di base degli accessi con scostamento (IX+d)", () => IxRegHex),
            new("IY", "Registro indice: indirizzo di base degli accessi con scostamento (IY+d)", () => IyRegHex),
            new("SP", "Puntatore alla cima dello stack", () => SpRegHex),
            new("PC", "Contatore di programma: indirizzo della prossima istruzione da eseguire", () => PcRegHex)
        ];

        Ez80Registers =
        [
            new("A", "Accumulatore: ci finisce il risultato della maggior parte delle operazioni aritmetiche e logiche", () => ARegHex),
            new("MB", "MBASE: i 16 bit alti dell'indirizzo quando l'eZ80 lavora in modo Z80", () => MbRegHex),
            new("STAT", "Stato riportato dall'interfaccia ZDI, dove fra l'altro si legge se la CPU e' ferma", () => StatRegHex),
            new("BC", "Coppia BC a 24 bit", () => Bc24Hex),
            new("DE", "Coppia DE a 24 bit", () => De24Hex),
            new("HL", "Coppia HL a 24 bit: l'indirizzo negli accessi indiretti a memoria", () => Hl24Hex),
            new("IX", "Registro indice: indirizzo di base degli accessi con scostamento (IX+d)", () => IxRegHex),
            new("IY", "Registro indice: indirizzo di base degli accessi con scostamento (IY+d)", () => IyRegHex),
            new("PC", "Contatore di programma: indirizzo della prossima istruzione da eseguire", () => PcRegHex),
            new("SPL", "Stack pointer lungo, a 24 bit: e' quello in uso in modo ADL", () => Spl24Hex),
            new("SPS", "Stack pointer corto, a 16 bit: e' quello in uso in modo Z80", () => Sps16Hex),
            new("MODO", "Modo di indirizzamento corrente: ADL a 24 bit oppure Z80 a 16 bit", () => AdlText),
            new("I", "Registro dei vettori di interruzione", () => IRegHex),
            new("R", "Contatore di rinfresco della memoria dinamica, incrementato a ogni prelievo di istruzione", () => RRegHex)
        ];

        _registerHighlightTimer = new DispatcherTimer { Interval = RegisterHighlightDuration };
        _registerHighlightTimer.Tick += (_, _) => ClearRegisterHighlights();
    }

    /// <summary>
    /// Riporta nelle celle i valori correnti, registri e flag del registro F insieme:
    /// arrivano dalla stessa rilettura e vanno confrontati con lo stesso passo di prima.
    /// Con <paramref name="highlight"/> acceso quelli diversi dalla lettura precedente
    /// restano evidenziati per qualche secondo:
    /// in una sessione di debug, dove si avanza un'istruzione alla volta, e' quello che
    /// dice cosa ha toccato l'istruzione appena eseguita. L'evidenza della lettura
    /// precedente si spegne qui: si riferisce a un passo che non e' piu' l'ultimo.
    /// </summary>
    private void RefreshRegisterCells(bool highlight)
    {
        // Puo' arrivarci una notifica prima che il costruttore abbia costruito le celle.
        if (_registerHighlightTimer is null)
        {
            return;
        }

        ClearRegisterHighlights();

        bool changed = false;

        foreach (var cell in Z80Registers)
        {
            changed |= cell.Refresh(highlight);
        }

        foreach (var cell in Ez80Registers)
        {
            changed |= cell.Refresh(highlight);
        }

        foreach (var flag in Flags)
        {
            changed |= flag.Refresh(FReg, highlight);
        }

        if (highlight && changed)
        {
            _registerHighlightTimer.Start();
        }
    }

    /// <summary>
    /// Da chiamare a fine rilettura, quando i registri sono tutti scritti. La prima
    /// lettura dopo l'apertura della porta non evidenzia niente: non c'e' un passo
    /// precedente, e mostrare tutto acceso non direbbe niente a nessuno.
    /// </summary>
    private void ApplyRegisterDump()
    {
        RefreshRegisterCells(highlight: _hasRegisterSnapshot);
        _hasRegisterSnapshot = true;
    }

    /// <summary>Spegne l'evidenza su tutte le celle e ferma il conto alla rovescia.</summary>
    private void ClearRegisterHighlights()
    {
        _registerHighlightTimer?.Stop();

        foreach (var cell in Z80Registers)
        {
            cell.IsChanged = false;
        }

        foreach (var flag in Flags)
        {
            flag.IsChanged = false;
        }

        foreach (var cell in Ez80Registers)
        {
            cell.IsChanged = false;
        }
    }

    /// <summary>
    /// Chiude il confronto in corso: la prossima lettura fa da nuovo punto di
    /// partenza. Serve quando la scheda se ne va o quando ne risponde un'altra,
    /// dove i registri di prima non appartengono piu' a niente.
    /// </summary>
    private void ResetRegisterComparison()
    {
        _hasRegisterSnapshot = false;
        ClearRegisterHighlights();
    }
    
    [ObservableProperty]
    private string _clockFrequency = DefaultClockFreq.ToString();

    /// <summary>
    /// Cambiare la frequenza la manda subito alla scheda. Il campo la conferma quando
    /// perde il fuoco o si preme Invio, non a ogni tasto premuto: il firmware, per
    /// applicare sclk, ferma e riavvia il clock e scrive il valore in EEPROM, quindi
    /// digitare "1000" a colpi di tasto vorrebbe dire quattro fermate della CPU e
    /// quattro scritture, tre delle quali su valori che nessuno ha chiesto.
    /// </summary>
    partial void OnClockFrequencyChanged(string value)
    {
        // Il valore arrivato con !INFO e' quello che la scheda ha gia': rimandarglielo
        // la farebbe fermare e ripartire appena connessa.
        if (_isApplyingCardConfig || !CanControlClock)
        {
            return;
        }

        if (int.TryParse(value, out var freq) && freq > 0)
        {
            _serialManager.SendSerialText($"sclk {freq}\r");
        }
    }
    
    [ObservableProperty]
    private bool _logEnabled;
    
    [ObservableProperty]
    private bool _debugEnabled;

    partial void OnDebugEnabledChanged(bool value) => NotifyDumpAvailabilityChanged();
    
    [RelayCommand]
    private void SendNmi()
    {
        _serialManager.SendSerialText("nmi\r");
    }

    [RelayCommand(CanExecute = nameof(CanReachBoard))]
    private void ReadMemory()
    {
        // uint e non ushort: la RAM dell'eZ80 sta a 0xFFF000, che in uno
        // ushort non entra. Per lo Z80 il comportamento non cambia.
        uint readSize = DefaultBytesRead;
        uint readStart = ParseAddressField(DefaultStartAdd);
        
        try
        {
            readSize = uint.Parse(NumBytes);
        }
        catch (FormatException)
        {
        }
        catch (OverflowException)
        {
        }
        
        _serialManager.ReadData(readStart, readSize);
    }

    [RelayCommand(CanExecute = nameof(CanReachBoard))]
    private void WriteMemory()
    {
        if (_fileBytesToWrite == null) return;
        
        uint writeStart = ParseAddressField(DefaultStartAdd);

        _serialManager.WriteData(_fileBytesToWrite, writeStart);
    }

    /// <summary>
    /// Leggere e scrivere la memoria vuole una scheda che abbia detto chi e': prima di
    /// allora il comando partirebbe alla cieca. Vale per tutti e due, che sono la stessa
    /// operazione in due versi.
    /// </summary>
    private bool CanReachBoard()
    {
        return IsBoardConnected;
    }

    /// <summary>
    /// Carica un binario nel dump di memoria. Sta fra i comandi della memoria e non
    /// fra quelli del sorgente perche' e' li' che i byte finiscono: sono gli stessi
    /// che "Scrivi" manda poi alla scheda.
    /// </summary>
    [RelayCommand]
    private async Task LoadBinary()
    {
        if (MainWindow is not { } window)
        {
            return;
        }

        var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Carica un binario in memoria",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Binari") { Patterns = ["*.bin"] },
                new FilePickerFileType("Tutti i file") { Patterns = ["*"] }
            ]
        });

        if (files.Count == 0)
        {
            return;
        }

        if (files[0].TryGetLocalPath() is { } path)
        {
            ProcessFile(path);
        }
    }

    
    [RelayCommand]
    private void Reset()
    {
        _serialManager.SendSerialText("reset\r");
    }
    
    [RelayCommand(CanExecute = nameof(CanUseDebug))]
    private void Debug()
    {
        DebugEnabled = !DebugEnabled;
        
        _serialManager.SendSerialText(DebugEnabled ? "debug on\r" : "debug off\r");
    }
    
    [RelayCommand]
    private void Step()
    {
        _serialManager.SendSerialText("s\r");
    }
    
    [RelayCommand(CanExecute = nameof(CanDumpRegisters))]
    private void Dump()
    {
        _serialManager.SendSerialText("d\r");
    }
    
    /// <summary>
    /// Avvia o ferma il clock. Non tocca piu' la frequenza: quella la manda il campo
    /// apposta quando la si cambia, cosi' i due comandi restano separati come i due
    /// comandi del firmware, e cambiare frequenza a clock fermo non lo fa partire.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanControlClock))]
    private void StartStopBoard()
    {
        if (IsClockActive)
        {
            _serialManager.SendSerialText("clk stop\r");
            IsClockActive = false;
        }
        else
        {
            // Il disassemblatore e i contatori del log del bus ripartono con il clock:
            // senza questo il log proseguirebbe dalla corsa precedente.
            _serialManager.RestartBoard();
            _serialManager.SendSerialText("clk start\r");
            IsClockActive = true;
        }
    }
    
    [RelayCommand(CanExecute = nameof(CanUseBusLog))]
    private void EnableDisableLogs()
    {
        LogEnabled = !LogEnabled;
        
        _serialManager.SendSerialText(LogEnabled ? "log on\r" : "log off\r");
    }
    
    [RelayCommand]
    private void ClearLogs()
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogTable.Clear();
        });
    }
    
    [RelayCommand]
    private void CloseTab(DocumentViewModel? tabToClose)
    {
        if (tabToClose != null && OpenDocuments.Contains(tabToClose))
        {
            StopWatchingDocument(tabToClose);
            OpenDocuments.Remove(tabToClose);

            foreach (var key in _listingDocuments
                         .Where(pair => pair.Value == tabToClose)
                         .Select(pair => pair.Key)
                         .ToArray())
            {
                _listingDocuments.Remove(key);
            }

            if (SelectedDocument == tabToClose)
            {
                SelectedDocument = OpenDocuments.FirstOrDefault();
            }
        }
    }
    
    private static string GetZ80FlagsString(byte f)
    {
        string sign = (f & 0x80) != 0 ? "S" : "NS";
        string zero = (f & 0x40) != 0 ? "Z" : "NZ";
        string halfCarry = (f & 0x10) != 0 ? "H" : "NH";
        string parityOverflow = (f & 0x04) != 0 ? "P/V" : "NP/V";
        string addSubtract = (f & 0x02) != 0 ? "N" : "A";
        string carry = (f & 0x01) != 0 ? "C" : "NC";

        return $"{sign} {zero} {halfCarry} {parityOverflow} {addSubtract} {carry}";
    }

    public MainViewModel(IDialogService dialogService)
    {
        // Prima di tutto il resto: le notifiche che partono dal costruttore passano
        // anche da qui, e le celle devono gia' esserci.
        InitializeRegisterCells();

        _dialogService = dialogService;
        
        _serialManager = new SerialManager(this);
        _serialManager.DataReceived += OnDataReceived;
        _serialManager.LogReceived += OnLogReceived;
        //_serialManager.LogProgress += OnLogProgress;
        _serialManager.LogMnemonicReceived += OnLogMnemonicReceived;
        _serialManager.DumpReceived += OnDumpReceived;
        _serialManager.Dump24Received += OnDump24Received;
        _serialManager.MessageReceived += OnMessageReceived;
        _serialManager.ConfigReceived += OnConfigReceived;
        
        DataTable = [];
        LogTable = [];
       
        //LogTable.Add(new LogElement (0x3000, 0xCC, false, false, false, false, false, false, "pluto"));
        
        PortsList = new ObservableCollection<string>();
        
        // Imposta il timer per controllare le porte seriali ogni secondo
        _serialPortCheckTimer = new Timer(1000);
        _serialPortCheckTimer.Elapsed += CheckSerialPorts;
        StartSerialPortMonitoring();
        
        NewTabCommand = new RelayCommand(_ => AddNewTab());
        OpenFileCommand = new RelayCommand(async _ => await OpenFileAsync());
        SaveFileCommand = new RelayCommand(async _ => await SaveFileAsync(), _ => SelectedDocument != null);
        SaveFileAsCommand = new RelayCommand(async _ => await SaveFileAsAsync(), _ => SelectedDocument != null);
        AssembleCommand = new RelayCommand(_ => RunAssembler(), _ => CanAssemble());
        SaveArduinoArrayCommand = new RelayCommand(async _ => await SaveArduinoArrayAsync(), _ => _fileBytesToWrite is { Length: > 0 });
        ToggleAssemblerOutputCommand = new RelayCommand(_ => IsAssemblerOutputVisible = !IsAssemblerOutputVisible);

        InitializeProjectCommands();
        InitializeDocks();

        //AddNewTab(); // apri una scheda iniziale
    }
    
    partial void OnIsClockFromArduinoChanged(bool value)
    {
        OnPropertyChanged(nameof(CanControlClock));
        StartStopBoardCommand.NotifyCanExecuteChanged();
        NotifyDumpAvailabilityChanged();

        if (_isApplyingCardConfig)
            return;

        if (value)
        {
            _serialManager.SetClockFromArduino();
        }
        else
        {
            _serialManager.SetClockExternal();
        }
    }
    
    partial void OnIsMemoryFromArduinoChanged(bool value)
    {
        NotifyDumpAvailabilityChanged();

        if (_isApplyingCardConfig)
            return;

        if (value)
        {
            _serialManager.SetMemoryFromArduino();
        }
        else
        {
            _serialManager.SetMemoryExternal();
        }
    }
    private void OnDataReceived(byte[] bytes)
    {
        uint readStart = ParseAddressField(DefaultStartAdd);
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            CreateArray(bytes, readStart);
        });
    }
    
    private void OnLogReceived(LogElement logElement)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogTable.Add(logElement);
        });
    }
    
    private void OnDumpReceived(byte aReg, byte bReg, byte cReg, byte dReg, byte eReg, byte hReg, byte lReg, ushort ixReg, ushort iyReg, ushort spReg, ushort pcReg, byte fReg)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            AReg = aReg;
            BReg = bReg;
            CReg = cReg;
            DReg = dReg;
            EReg = eReg;
            HReg = hReg;
            LReg = lReg;
            IxReg = ixReg;
            IyReg = iyReg;
            SpReg = spReg;
            PcReg = pcReg;
            FReg = fReg;

            ApplyRegisterDump();
        });
    }
    
    private void OnDump24Received(Ez80Registers registers)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            IsEz80 = true;

            AReg = registers.A;
            FReg = registers.F;
            MbReg = registers.Mbase;
            StatReg = registers.Stat;
            IsAdl = registers.IsAdl;
            IsCpuHalted = registers.IsHalted;

            Bc24 = registers.Bc;
            De24 = registers.De;
            Hl24 = registers.Hl;
            Ix24 = registers.Ix;
            Iy24 = registers.Iy;
            Spl24 = registers.Spl;
            Sps16 = registers.Sps;
            Pc24 = registers.Pc;

            // Le meta' a 8 bit restano popolate: sono le stesse celle che la
            // vista Z80 mostra, e su eZ80 corrispondono ai byte bassi.
            BReg = (byte)(registers.Bc >> 8);
            CReg = (byte)registers.Bc;
            DReg = (byte)(registers.De >> 8);
            EReg = (byte)registers.De;
            HReg = (byte)(registers.Hl >> 8);
            LReg = (byte)registers.Hl;

            IReg = registers.I;
            RReg = registers.R;
            Iff2 = registers.Iff2;

            ApplyRegisterDump();
        });
    }

    /*private void OnLogProgress(int progress)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            ProgressValue = progress;
            ProgressVisible = progress < 100;
            if (progress == 100)
            { 
                DataTable.Clear();  // Al termine della scrittura cancella l'area dati
            }
        });
    }*/
    
    private void OnLogMnemonicReceived(int idx, string mnemonic)
    {
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            LogTable[idx].Mnemonic = mnemonic;
        });
    }
    
    private void OnMessageReceived(string msg)
    {
        Dispatcher.UIThread.Post(() => AppendSerialLine(msg));
    }
    
    private void OnConfigReceived(CardConfig cardConfig)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _isApplyingCardConfig = true;
            try
            {
                CardName = cardConfig.CardName;
                IsEz80 = cardConfig.IsEz80;
                IsAlex80Full = cardConfig.IsAlex80Full;
                IsAlex80Micro = cardConfig.IsAlex80Micro;
                OnPropertyChanged(nameof(RunToolTip));
                OnPropertyChanged(nameof(CanRun));
                IsClockFromArduino = cardConfig.IsClockFromArduino;
                IsMemoryFromArduino = cardConfig.IsMemFromArduino;
                ClockFrequency = cardConfig.ClockFrequency.ToString();
                IsClockActive = cardConfig.ClockActive;
                AppendSerialLine($"UI config: CARD={CardName}, IsSerialOpened={IsSerialOpened}");
            }
            finally
            {
                _isApplyingCardConfig = false;
            }
        });
    }
    
    private void StartSerialPortMonitoring() {
        _serialPortCheckTimer.Start();
    }

    private void CheckSerialPorts(object? sender, ElapsedEventArgs e) {
        // Ottieni le porte seriali attualmente disponibili
        try
        {
            var availablePorts = SerialPortHelper.GetSerialPorts();
            Dispatcher.UIThread.Post(() =>
            {
                if (availablePorts.SequenceEqual(PortsList))
                    return;

                var openPortName = _serialManager.SerialPort.IsOpen
                    ? _serialManager.SerialPort.PortName
                    : null;
                var selectedPortName = SelectedIdx >= 0 && SelectedIdx < PortsList.Count
                    ? PortsList[SelectedIdx]
                    : null;

                // Chiude la seriale solo se la porta realmente aperta è scomparsa.
                if (openPortName != null && !availablePorts.Contains(openPortName))
                {
                    _serialManager.CloseSerialPort();
                    ToggleButtonContent = "Apri";
                }

                PortsList.Clear();
                foreach (var port in availablePorts)
                    PortsList.Add(port);

                if (openPortName != null && availablePorts.Contains(openPortName))
                    SelectedIdx = Array.IndexOf(availablePorts, openPortName);
                else if (selectedPortName != null && availablePorts.Contains(selectedPortName))
                    SelectedIdx = Array.IndexOf(availablePorts, selectedPortName);
                else
                    SelectedIdx = availablePorts.Length > 0 ? 0 : -1;
            });
        }
        catch (Exception ex)
        {
            // Gestisci eventuali errori
            Console.WriteLine($"Errore: {ex.Message}");
        }
    }

    private void AddNewTab()
    {
        var doc = new DocumentViewModel
        {
            FileName = "Nuovo.asm",
            Text = ";;; Codice ASM per Alex80\n"
        };
        doc.IsDirty = true;
        OpenDocuments.Add(doc);
        SelectedDocument = doc;
    }
    
    private async Task OpenFileAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Assembly Z80", Extensions = { "asm", "z80", "txt" } }
            },
            AllowMultiple = false
        };

        var result = await dialog.ShowAsync(App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);

        if (result != null && result.Length > 0)
        {
            var path = result[0];
            var existingDocument = FindOpenDocument(path);
            if (existingDocument is not null)
            {
                SelectedDocument = existingDocument;
                return;
            }

            var text = await File.ReadAllTextAsync(path);

            var doc = new DocumentViewModel
            {
                FileName = Path.GetFileName(path),
                FilePath = path,
                Text = text
            };
            doc.IsDirty = false;

            OpenDocuments.Add(doc);
            SelectedDocument = doc;
            StartWatchingDocument(doc);
        }
    }

    private async Task SaveFileAsync()
    {
        if (SelectedDocument == null) return;

        if (!string.IsNullOrWhiteSpace(SelectedDocument.FilePath))
        {
            await File.WriteAllTextAsync(SelectedDocument.FilePath, SelectedDocument.Text);
            SelectedDocument.IsDirty = false;
            return;
        }

        await SaveFileAsAsync();
    }

    private async Task SaveFileAsAsync()
    {
        if (SelectedDocument == null) return;

        var desktop = App.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        if (desktop?.MainWindow is not { } window)
            return;

        var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salva codice assembler",
            SuggestedFileName = SelectedDocument.FileName,
            ShowOverwritePrompt = true
        });

        if (file is not null)
        {
            var path = EnsureAssemblyExtension(file.Path.LocalPath);
            await File.WriteAllTextAsync(path, SelectedDocument.Text);
            SelectedDocument.MarkSaved(path);
            StartWatchingDocument(SelectedDocument);
        }
    }

    private static string EnsureAssemblyExtension(string filePath)
    {
        if (!Path.HasExtension(filePath))
            return filePath + ".asm";

        foreach (var extension in new[] { ".asm", ".z80", ".txt" })
        {
            while (filePath.EndsWith(extension + extension, StringComparison.OrdinalIgnoreCase))
                filePath = filePath[..^extension.Length];
        }

        return filePath;
    }

    private async Task SaveArduinoArrayAsync()
    {
        if (_fileBytesToWrite is not { Length: > 0 } bytes)
            return;

        var arrayName = GetOutputBaseName();
        var dialog = new SaveFileDialog
        {
            InitialFileName = arrayName + ".h",
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Arduino header", Extensions = { "h" } },
                new FileDialogFilter { Name = "Text file", Extensions = { "txt" } }
            }
        };

        var path = await dialog.ShowAsync(App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);

        if (!string.IsNullOrWhiteSpace(path))
            await File.WriteAllTextAsync(path, FormatArduinoArray(bytes, arrayName));
    }

    /// <summary>Nome base per i file prodotti: quello del progetto, o del sorgente selezionato.</summary>
    private string GetOutputBaseName()
    {
        var name = CurrentProject?.DisplayName
                   ?? (SelectedDocument is null ? null : Path.GetFileNameWithoutExtension(SelectedDocument.FileName));

        if (string.IsNullOrWhiteSpace(name))
        {
            return "intROM";
        }

        // Deve essere un identificatore C valido, visto che finisce nel nome dell'array.
        var sanitized = new string(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        return char.IsDigit(sanitized[0]) ? "_" + sanitized : sanitized;
    }

    private static string FormatArduinoArray(byte[] bytes, string arrayName)
    {
        const int bytesPerLine = 16;
        var builder = new StringBuilder($"const PROGMEM byte {arrayName}[] = {{\n");

        for (var index = 0; index < bytes.Length; index += bytesPerLine)
        {
            var count = Math.Min(bytesPerLine, bytes.Length - index);
            builder.Append("    ");
            builder.Append(string.Join(", ", bytes.Skip(index).Take(count).Select(value => $"0x{value:X2}")));

            if (index + count < bytes.Length)
                builder.Append(',');

            builder.AppendLine();
        }

        builder.AppendLine("};");
        return builder.ToString();
    }

    private DocumentViewModel? FindOpenDocument(string filePath)
    {
        var normalizedPath = NormalizeFilePath(filePath);

        return OpenDocuments.FirstOrDefault(document =>
            !string.IsNullOrWhiteSpace(document.FilePath) &&
            string.Equals(NormalizeFilePath(document.FilePath), normalizedPath, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeFilePath(string filePath)
    {
        return Path.GetFullPath(filePath);
    }

    private void StartWatchingDocument(DocumentViewModel document)
    {
        StopWatchingDocument(document);

        if (string.IsNullOrWhiteSpace(document.FilePath))
        {
            return;
        }

        var directory = Path.GetDirectoryName(document.FilePath);
        var fileName = Path.GetFileName(document.FilePath);

        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
        {
            return;
        }

        var watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        watcher.Changed += (_, _) => ScheduleDocumentReload(document);
        watcher.Created += (_, _) => ScheduleDocumentReload(document);
        watcher.Renamed += (_, _) => ScheduleDocumentReload(document);

        lock (_fileWatcherLock)
        {
            _fileWatchers[document] = watcher;
        }
    }

    private void StopWatchingDocument(DocumentViewModel document)
    {
        lock (_fileWatcherLock)
        {
            if (_reloadDebounceTokens.Remove(document, out var tokenSource))
            {
                tokenSource.Cancel();
                tokenSource.Dispose();
            }

            if (_fileWatchers.Remove(document, out var watcher))
            {
                watcher.Dispose();
            }
        }
    }

    private void ScheduleDocumentReload(DocumentViewModel document)
    {
        lock (_fileWatcherLock)
        {
            if (_reloadDebounceTokens.Remove(document, out var previousTokenSource))
            {
                previousTokenSource.Cancel();
                previousTokenSource.Dispose();
            }

            var tokenSource = new CancellationTokenSource();
            _reloadDebounceTokens[document] = tokenSource;

            _ = ReloadDocumentAfterDelayAsync(document, tokenSource);
        }
    }

    private async Task ReloadDocumentAfterDelayAsync(DocumentViewModel document, CancellationTokenSource tokenSource)
    {
        try
        {
            await Task.Delay(250, tokenSource.Token);

            if (string.IsNullOrWhiteSpace(document.FilePath) || !File.Exists(document.FilePath))
            {
                return;
            }

            var text = await ReadAllTextWithRetryAsync(document.FilePath, tokenSource.Token);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (OpenDocuments.Contains(document) && document.Text != text)
                {
                    document.ReloadFromDisk(text);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Impossibile aggiornare il file aperto: {ex.Message}");
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine($"Impossibile aggiornare il file aperto: {ex.Message}");
        }
        finally
        {
            lock (_fileWatcherLock)
            {
                if (_reloadDebounceTokens.TryGetValue(document, out var currentTokenSource) && currentTokenSource == tokenSource)
                {
                    _reloadDebounceTokens.Remove(document);
                }
            }

            tokenSource.Dispose();
        }
    }

    private static async Task<string> ReadAllTextWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await File.ReadAllTextAsync(filePath, cancellationToken);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(100, cancellationToken);
            }
        }

        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }

    private static bool IsZ80Source(DocumentViewModel? document)
    {
        if (document is null || string.IsNullOrWhiteSpace(document.FileName))
        {
            return false;
        }

        var extension = Path.GetExtension(document.FileName);
        return string.Equals(extension, ".asm", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".z80", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".inc", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private bool CanAssemble() => CurrentProject is not null || IsZ80Source(SelectedDocument);

    [RelayCommand]
    private async Task OpenCloseSerialPort()
    {
        try
        {
            SerialMessageText = string.Empty;
            bool isOpen = await _serialManager.OpenCloseSerialPortAsync(PortsList[SelectedIdx]);
            ToggleButtonContent = isOpen ? "Chiudi" : "Apri";

            if (isOpen)
            {
                LogEnabled = false;
                DebugEnabled = false;
            }
    
        }
        catch (UnauthorizedAccessException ex)
        {
            // La porta esiste ma non hai i permessi
            IsSerialOpened = false;
            
            Console.WriteLine($"Accesso negato alla porta {PortsList[SelectedIdx]}: {ex.Message}");
            await _dialogService.ShowErrorAsync("Accesso negato", $"Non è stato possibile accedere alla porta {PortsList[SelectedIdx]}. Potrebbe essere già aperta in un altro programma.");
        }
        catch (IOException ex)
        {
            IsSerialOpened = false;
            
            Console.WriteLine($"Errore di I/O sulla porta {PortsList[SelectedIdx]}: {ex.Message}");
            await _dialogService.ShowErrorAsync("Errore Porta Seriale", $"Errore di I/O sulla porta {PortsList[SelectedIdx]}");
        }
        catch (Exception ex)
        {
            IsSerialOpened = false;
            
            Console.WriteLine($"Errore imprevisto durante l'apertura della porta {PortsList[SelectedIdx]}: {ex.Message}");
            await _dialogService.ShowErrorAsync("Errore Imprevisto", $"Si è verificato un errore imprevisto durante l'operazione sulla porta seriale {PortsList[SelectedIdx]}.");
        }
    }
    
    public void ProcessFile(string filePath)
    {
        _fileBytesToWrite = FileHelper.GetFile(filePath);
        (SaveArduinoArrayCommand as RelayCommand)?.RaiseCanExecuteChanged();

        if (_fileBytesToWrite != null)
        {
            uint writeStart = ParseAddressField(DefaultStartAdd);
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                NumBytes = _fileBytesToWrite.Length.ToString();
                CreateArray(_fileBytesToWrite, writeStart); 
            });
        }
        else
        {
            Console.WriteLine("Impossibile leggere il file.");
        }
    }
    
    private void CreateArray(byte[] bytes, uint start = 0)
    {
        DataTable.Clear();

        List<byte> byteArray = new List<byte>();
        string asciiString = "";

        for (int i = 0; i < bytes.Length; i++)
        {
            byteArray.Add(bytes[i]);

            if (IsPrintable((char)bytes[i]))
            {
                asciiString += (char)bytes[i];
            }
            else
            {
                asciiString += ".";
            }
          
            uint address = (uint)(start + ((i / 16) * 16)) & 0xFFFFFF;
            if ((i + 1) % 16 == 0)
            {
                var bankRow = new RowElement(address, new List<byte>(byteArray), asciiString, UseWideAddresses);

                DataTable.Add(bankRow);

                byteArray.Clear();
                asciiString = "";
            }
            else if (i == bytes.Length - 1 && (i + 1) % 16 != 0)
            {
                //var bankRow = new RowElement((start + (ushort)((i / 16) * 16)) % 65536, new List<byte>(byteArray), asciiString);
                var bankRow = new RowElement(address, new List<byte>(byteArray), asciiString, UseWideAddresses);
                
                DataTable.Add(bankRow);
            }
        }
    }
    
    private static bool IsPrintable(char c)
    {
        return !char.IsControl(c);
    }
    
}
