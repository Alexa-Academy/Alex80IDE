using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using Alex80_IDE;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Konamiman.Nestor80.Assembler;
using System.Linq;
using System.Text;
using System.Timers;
using Alex80_IDE.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    
    private const int DefaultClockFreq = 50;
    private const int DefaultStartAdd = 0x0000;
    private const int DefaultBytesRead = 80;
    
    public ObservableCollection<DocumentViewModel> OpenDocuments { get; } = new();
    private DocumentViewModel _selectedDocument;
    public ICommand OpenFileCommand { get; }
    public ICommand SaveFileCommand { get; }
    public DocumentViewModel SelectedDocument
    {
        get => _selectedDocument;
        set { _selectedDocument = value; OnPropertyChanged(); }
    }

    public ICommand NewTabCommand { get; }
    public ICommand AssembleCommand { get; }
    
    [ObservableProperty]
    private string _cardName;
    
    [ObservableProperty]
    private string _assembledOutput;
    
    [ObservableProperty]
    private string _lineByLineOutput;

    [ObservableProperty]
    private bool _isAssemblerOutputVisible = true;
    
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
    
    [ObservableProperty] 
    private bool _isMemoryFromArduino;
    
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
    
    [ObservableProperty]
    private ushort _ixReg;
    
    public string IxRegHex => IxReg.ToString("X4");
    partial void OnIxRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(IxRegHex));
    }
    
    [ObservableProperty]
    private ushort _iyReg;
    
    public string IyRegHex => IyReg.ToString("X4");
    partial void OnIyRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(IyRegHex));
    }
    
    [ObservableProperty]
    private ushort _spReg;
    
    public string SpRegHex => SpReg.ToString("X4");
    partial void OnSpRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(SpRegHex));
    }
    
    [ObservableProperty]
    private ushort _pcReg;
    
    public string PcRegHex => PcReg.ToString("X4");
    partial void OnPcRegChanged(ushort value)
    {
        OnPropertyChanged(nameof(PcRegHex));
    }
    
    [ObservableProperty]
    private byte _fReg;
    
    public string FlagsString => GetZ80FlagsString(FReg);
    
    partial void OnFRegChanged(byte value)
    {
        OnPropertyChanged(nameof(FlagsString));
    }
    
    [ObservableProperty]
    private string _clockFrequency = DefaultClockFreq.ToString();
    
    [ObservableProperty]
    private bool _logEnabled;
    
    [ObservableProperty]
    private bool _debugEnabled;
    
    [RelayCommand]
    private void SendNmi()
    {
        _serialManager.SendSerialText("nmi\r");
    }

    [RelayCommand]
    private void ReadMemory()
    {
        var readSize = DefaultBytesRead;
        var readStart = DefaultStartAdd;
        
        try
        {
            readSize = ushort.Parse(NumBytes);
            readStart = ushort.Parse(AddressHex);
        }
        catch (FormatException)
        {
        }
        
        _serialManager.ReadData((ushort)readStart, (ushort)readSize);
    }

    [RelayCommand]
    private void WriteMemory()
    {
        if (_fileBytesToWrite == null) return;
        
        ushort writeStart = DefaultStartAdd;
        
        try
        {
            writeStart = ushort.Parse(AddressHex);
        }
        catch (FormatException)
        {
        }

        _serialManager.WriteData(_fileBytesToWrite, writeStart);
    }
    
    [RelayCommand]
    private void Reset()
    {
        _serialManager.SendSerialText("reset\r");
    }
    
    [RelayCommand]
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
    
    [RelayCommand] 
    private void Dump()
    {
        _serialManager.SendSerialText("d\r");
    }
    
    [RelayCommand]
    private async Task StartStopBoard()
    {
        if (IsClockActive)
        {
            _serialManager.SendSerialText("clk stop\r");
            IsClockActive = false;
        }
        else
        {
            _serialManager.RestartBoard();
       
            int freq = DefaultClockFreq;
            try
            {
                freq = int.Parse(ClockFrequency);
            }
            catch (FormatException)
            {
            }
       
            _serialManager.SendSerialText($"sclk {freq}\r");
            await Task.Delay(400);
            _serialManager.SendSerialText("clk start\r");

            IsClockActive = true;
        }
    }
    
    [RelayCommand]
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
            OpenDocuments.Remove(tabToClose);

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
        _dialogService = dialogService;
        
        _serialManager = new SerialManager(this);
        _serialManager.DataReceived += OnDataReceived;
        _serialManager.LogReceived += OnLogReceived;
        //_serialManager.LogProgress += OnLogProgress;
        _serialManager.LogMnemonicReceived += OnLogMnemonicReceived;
        _serialManager.DumpReceived += OnDumpReceived;
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
       // AssembleCommand = new RelayCommand(_ => RunAssembler(), _ => SelectedDocument != null);
        AssembleCommand = new RelayCommand(_ => RunAssembler());
        ToggleAssemblerOutputCommand = new RelayCommand(_ => IsAssemblerOutputVisible = !IsAssemblerOutputVisible);
        //AddNewTab(); // apri una scheda iniziale
    }
    
    partial void OnIsClockFromArduinoChanged(bool value)
    {
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
        ushort readStart = DefaultStartAdd;
        try
        {
            readStart = ushort.Parse(AddressHex, System.Globalization.NumberStyles.HexNumber);
        }
        catch (FormatException)
        {
        }
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
        AppendSerialLine(msg);
    }
    
    private void OnConfigReceived(CardConfig cardConfig)
    {
        CardName = cardConfig.CardName;
        IsClockFromArduino = cardConfig.IsClockFromArduino;
        IsMemoryFromArduino = cardConfig.IsMemFromArduino;
        ClockFrequency = cardConfig.ClockFrequency.ToString();
        IsClockActive = cardConfig.ClockActive;
    }
    
    private void StartSerialPortMonitoring() {
        _serialPortCheckTimer.Start();
    }

    private async void CheckSerialPorts(object? sender, ElapsedEventArgs e) {
        // Ottieni le porte seriali attualmente disponibili
        try
        {
            var availablePorts = SerialPortHelper.GetSerialPorts();
            if (availablePorts.SequenceEqual(PortsList))
                return;

            if (SelectedIdx >= availablePorts.Length || (SelectedIdx >= 0 && SelectedIdx < availablePorts.Length &&
                                                         _serialManager.SerialPort.PortName == PortsList[SelectedIdx]))
            {
                ToggleButtonContent = _serialManager.CloseSerialPort() ? "Chiudi" : "Apri";
            }


            // Controlla se ci sono cambiamenti nella lista delle porte
            //SelectedIdx = -1;
            if (availablePorts.Any())
            {
                PortsList.Clear();
                foreach (var port in availablePorts)
                {
                    PortsList.Add(port);
                }

                // Forza l'aggiornamento visivo
                await Task.Delay(10);

                SelectedIdx = 0;

                OnPropertyChanged(nameof(SelectedIdx));
            }
            else
            {
                SelectedIdx = -1;
            }
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
            var text = await File.ReadAllTextAsync(path);

            var doc = new DocumentViewModel
            {
                FileName = Path.GetFileName(path),
                Text = text
            };

            OpenDocuments.Add(doc);
            SelectedDocument = doc;
        }
    }

    private async Task SaveFileAsync()
    {
        if (SelectedDocument == null) return;

        var dialog = new SaveFileDialog
        {
            InitialFileName = SelectedDocument.FileName,
            Filters = new List<FileDialogFilter>
            {
                new FileDialogFilter { Name = "Assembly Z80", Extensions = { "asm", "z80", "txt" } }
            }
        };

        var path = await dialog.ShowAsync(App.Current.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);

        if (!string.IsNullOrWhiteSpace(path))
        {
            await File.WriteAllTextAsync(path, SelectedDocument.Text);
            SelectedDocument.FileName = Path.GetFileName(path);
        }
    }
    
    private void RunAssembler()
    {
        try
        {
            var sourceCode = SelectedDocument.Text;

            var config = new AssemblyConfiguration
            {
                CpuName = "Z80",
                BuildType = BuildType.Absolute,
                MaxErrors = 10
            };

            using var sourceStream = new MemoryStream(Encoding.UTF8.GetBytes(sourceCode));
            var result = AssemblySourceProcessor.Assemble(sourceStream, Encoding.UTF8, config);

            if (result.HasErrors)
            {
                var errors = string.Join(Environment.NewLine,
                    result.Errors.Select(e => $"{e.LineNumber}: {(e.IsWarning ? "Warning" : "Error")} - {e.Message}"));

                var errorTab = new DocumentViewModel
                {
                    FileName = "Errori assembler",
                    Text = errors
                };

                OpenDocuments.Add(errorTab);
                SelectedDocument = errorTab;
                return;
            }
            
            // Generazione del binario
            using var outputStream = new MemoryStream();
            var numBytes = OutputGenerator.GenerateAbsolute(result, outputStream);
            _fileBytesToWrite = outputStream.ToArray();
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddressHex = result.FirstAddress.ToString("X4");
                NumBytes = numBytes.ToString();
                CreateArray(_fileBytesToWrite, (ushort)result.FirstAddress);
            });

            using var listingStream = new MemoryStream();
            using var writer = new StreamWriter(listingStream, Encoding.UTF8, leaveOpen: true);
            var listingConfig = new ListingFileConfiguration();
            ListingFileGenerator.GenerateListingFile(result, writer, listingConfig);
            writer.Flush();

            listingStream.Position = 0;
            using var reader = new StreamReader(listingStream);
            var listingText = reader.ReadToEnd();

            var listingTab = new DocumentViewModel
            {
                FileName = "Listing assembler",
                Text = listingText
            };

            OpenDocuments.Add(listingTab);
            SelectedDocument = listingTab;
        }
        catch (Exception ex)
        {
            var errorTab = new DocumentViewModel
            {
                FileName = "Errore",
                Text = $"Errore: {ex.Message}"
            };
            OpenDocuments.Add(errorTab);
            SelectedDocument = errorTab;
        }
    }
    
    [RelayCommand]
    private async Task OpenCloseSerialPort()
    {
        try
        {
            bool isOpen = await _serialManager.OpenCloseSerialPortAsync(PortsList[SelectedIdx]);
            ToggleButtonContent = isOpen ? "Chiudi" : "Apri";

            if (isOpen)
            {
                LogEnabled = false;
                DebugEnabled = false;
                
                SerialMessageText = string.Empty;
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

        if (_fileBytesToWrite != null)
        {
            ushort writeStart = DefaultBytesRead;
            try
            {
               writeStart = ushort.Parse(AddressHex, System.Globalization.NumberStyles.HexNumber);
            }
            catch (FormatException)
            {
            }
            
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                CreateArray(_fileBytesToWrite, writeStart); 
            });
        }
        else
        {
            Console.WriteLine("Impossibile leggere il file.");
        }
    }
    
    private void CreateArray(byte[] bytes, ushort start = 0)
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
          
            ushort address = (ushort)(start + ((i / 16) * 16));
            if ((i + 1) % 16 == 0)
            {
                var bankRow = new RowElement(address, new List<byte>(byteArray), asciiString);

                DataTable.Add(bankRow);

                byteArray.Clear();
                asciiString = "";
            }
            else if (i == bytes.Length - 1 && (i + 1) % 16 != 0)
            {
                //var bankRow = new RowElement((start + (ushort)((i / 16) * 16)) % 65536, new List<byte>(byteArray), asciiString);
                var bankRow = new RowElement(address, new List<byte>(byteArray), asciiString);
                
                DataTable.Add(bankRow);
            }
        }
    }
    
    private static bool IsPrintable(char c)
    {
        return !char.IsControl(c);
    }
    
}