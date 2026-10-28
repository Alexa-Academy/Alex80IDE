using System.Linq;
using Alex80_IDE.Models;
using Alex80_IDE.ViewModels;
using System.IO.Ports;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Alex80Supervisor;
using Alex80_IDE.Services;
using Avalonia.Threading;

namespace Alex80_IDE;

public enum State {
    STATE_WAIT_AA,
    STATE_WAIT_COMMAND,
    STATE_WAIT_LENGTH_HIGH,
    STATE_WAIT_LENGTH_LOW,
    STATE_WAIT_BYTE1,
    STATE_WAIT_BYTE2,
    STATE_WAIT_DATA
}

/// <summary>
/// Registri eZ80 come arrivano nel messaggio !DUMP24: valori a 24 bit interi,
/// piu' cio' che il formato Z80 a 16 bit non puo' rappresentare (MBASE, STAT,
/// stack pointer corto, I, R, IFF2).
/// </summary>
public readonly record struct Ez80Registers(
    byte A, byte F, byte Mbase, byte Stat,
    uint Bc, uint De, uint Hl, uint Ix, uint Iy, uint Spl,
    ushort Sps, uint Pc,
    byte I, byte R, bool Iff2)
{
    /// <summary>Bit 4 di STAT: la CPU sta girando in modalita' ADL.</summary>
    public bool IsAdl => (Stat & 0x10) != 0;
    /// <summary>Bit 7 di STAT: la CPU e' ferma in break ZDI.</summary>
    public bool IsHalted => (Stat & 0x80) != 0;
}

public class SerialManager {
    private readonly SerialPort _serialPort;
    public SerialPort SerialPort => _serialPort;
    
    private readonly MainViewModel mainViewModel;

    private readonly Disassembler disassembler = new();
    
    private State smState = State.STATE_WAIT_AA;
    private int cmdLength;
    private int smTemp;
 //   private int currentAddress;
    private int chunkNum;

    private LogElement? instructionStartLog;
    private bool dataFinished;

    private byte command;
    private readonly List<byte> dataArray;
    private List<byte> writeDataArray;
 //   private readonly List<byte> tmpDataArray;
    
    public event Action<byte[]>? DataReceived;
    public event Action<LogElement>? LogReceived; 
    public event Action<int>? LogProgress;
    public event Action<LogElement, string>? LogMnemonicReceived;
    public event Action<byte, byte, byte, byte, byte, byte, byte, ushort, ushort, ushort, ushort, byte>? DumpReceived; 
    public event Action<Ez80Registers>? Dump24Received; 
    public event Action<string>? MessageReceived;
    public event Action<CardConfig>? ConfigReceived;
   
    private string _serialBuffer = "";
    private readonly object _serialReadLock = new();

    private enum ReadPurpose { None, Memory, Instruction }
    private ReadPurpose readPurpose;
    private bool discardLateInstructionResponse;
    private uint instructionAddress, instructionChunkAddress, instructionWrapMask;
    private int instructionChunkLength;
    private bool instructionEz80;
    private readonly List<byte> instructionBytes = new();
    private CancellationTokenSource? instructionTimeout;
    public bool IsReadPending => readPurpose != ReadPurpose.None;
    public event Action? ReadStateChanged;
    public event Action<uint, byte[]>? InstructionBytesReceived;
    public event Action<uint, string>? InstructionReadFailed;

    public bool ReadInstruction(uint address, bool ez80, bool adl)
    {
        if (!_serialPort.IsOpen || IsReadPending) return false;
        discardLateInstructionResponse = false;
        instructionAddress = instructionChunkAddress = address;
        instructionEz80 = ez80;
        instructionWrapMask = ez80 && adl ? 0xFFFFFFu : 0xFFFFu;
        instructionBytes.Clear(); readPurpose = ReadPurpose.Instruction;
        ReadStateChanged?.Invoke();
        instructionTimeout?.Cancel();
        instructionTimeout = new CancellationTokenSource();
        _ = ExpireInstructionReadAsync(instructionTimeout.Token);
        SendInstructionChunk();
        return true;
    }
    private async Task ExpireInstructionReadAsync(CancellationToken token)
    {
        // Native USB ports may not notify DataReceived promptly. Poll the same
        // locked receive path while a short PC read is pending.
        try
        {
            for (int elapsed = 0; elapsed < 5000; elapsed += 100)
            {
                await Task.Delay(100, token);
                if (token.IsCancellationRequested || readPurpose != ReadPurpose.Instruction || !_serialPort.IsOpen) return;
                ReadAvailableSerialData();
            }
        }
        catch (OperationCanceledException) { return; }
        if (!token.IsCancellationRequested && readPurpose == ReadPurpose.Instruction)
            FailInstructionRead("Timeout nella lettura dei byte al PC.");
    }
    private void SendInstructionChunk()
    {
        instructionChunkLength = (int)Math.Min((uint)(16 - instructionBytes.Count),
            instructionWrapMask - (instructionChunkAddress & instructionWrapMask) + 1);
        totalLength = instructionChunkLength; length = 0;
        SendSerialText($"rb {FormatMemoryAddress(instructionChunkAddress, instructionEz80)} {instructionChunkLength}\r");
    }
    private void FinishRead()
    {
        var purpose = readPurpose;
        if (purpose == ReadPurpose.Instruction)
        {
            if (dataArray.Count != instructionChunkLength) { FailInstructionRead("Risposta memoria incompleta."); return; }
            instructionBytes.AddRange(dataArray);
            if (instructionBytes.Count < 16)
            {
                instructionChunkAddress = (instructionAddress & ~instructionWrapMask) |
                    ((instructionChunkAddress + (uint)instructionChunkLength) & instructionWrapMask);
                SendInstructionChunk(); return;
            }
        }
        readPurpose = ReadPurpose.None;
        instructionTimeout?.Cancel(); ReadStateChanged?.Invoke();
        if (purpose == ReadPurpose.Instruction) InstructionBytesReceived?.Invoke(instructionAddress, instructionBytes.ToArray());
        else if (purpose == ReadPurpose.Memory || !discardLateInstructionResponse) DataReceived?.Invoke(dataArray.ToArray());
        discardLateInstructionResponse = false;
    }
    private void FailInstructionRead(string message)
    {
        readPurpose = ReadPurpose.None; instructionTimeout?.Cancel(); discardLateInstructionResponse = true;
        ReadStateChanged?.Invoke(); InstructionReadFailed?.Invoke(instructionAddress, message);
    }
    public void CancelReadForReset()
    {
        bool hadRead = IsReadPending;
        readPurpose = ReadPurpose.None; instructionTimeout?.Cancel();
        discardLateInstructionResponse = hadRead;
        ReadStateChanged?.Invoke();
    }
    private void ClearPendingRead()
    {
        readPurpose = ReadPurpose.None; instructionTimeout?.Cancel(); dataArray.Clear(); discardLateInstructionResponse = false;
        ReadStateChanged?.Invoke();
    }

    private int totalLength;
    private int length;
    
    public SerialManager(MainViewModel mainViewModel)
    {
        dataArray = [];
       // tmpDataArray = [];
        
        this.mainViewModel = mainViewModel;

        mainViewModel.IsSerialOpened = false;
        
        _serialPort = new SerialPort {
              BaudRate = mainViewModel.SelectedBaudRate, // Velocità di trasmissione
              Parity = Parity.None,       // Parità
              DataBits = 8,               // Bit di dati
              StopBits = StopBits.One,    // Bit di stop
              Handshake = Handshake.None, // Handshake
              DtrEnable = true,
              RtsEnable = true
         };
         _serialPort.Encoding = Encoding.UTF8;
        
         _serialPort.DataReceived += DataReceivedHandler;
       // Task.Run(() => { ReadFromSerialPortAsync(); });
       
      /*  Console.WriteLine(disassembler.Disassemble(pc: 0x8000, b: 0));
        Console.WriteLine(disassembler.Disassemble(pc: 0x8001, b: 0x8e));
        
        Console.WriteLine(disassembler.Disassemble(pc: 0x8002, b: 0xce));
        Console.WriteLine(disassembler.Disassemble(pc: 0x8003, b: 0x54));
        
        Console.WriteLine(disassembler.Disassemble(pc: 0x8004, b: 0xce));
        Console.WriteLine(disassembler.Disassemble(pc: 0x8005, b: 0xF4)); 
        
        Console.WriteLine($"> {disassembler.Disassemble(pc: 0x8006, b: 0xed)}");
        Console.WriteLine($"> {disassembler.Disassemble(pc: 0x8007, b: 0x7b)}");
        Console.WriteLine($"> {disassembler.Disassemble(pc: 0x8008, b: 0xf8)}");
        Console.WriteLine($"> {disassembler.Disassemble(pc: 0x8009, b: 0xd2)}");
        
        Console.WriteLine($"> {disassembler.Disassemble(7, 0)}");
        Console.WriteLine($"> {disassembler.Disassemble(7, 0)}");*/
    }
    
    private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
    {
        ReadAvailableSerialData();
    }

    private void ReadAvailableSerialData()
    {
        if (!_serialPort.IsOpen)
            return;

        lock (_serialReadLock)
        {
            try
            {
                if (_serialPort.BytesToRead == 0)
                    return;

                string incoming = _serialPort.ReadExisting();
                if (incoming.Contains("!INFO"))
                {
                    MessageReceived?.Invoke($"RX raw: {FormatSerialText(incoming)}");
                }

                _serialBuffer += incoming;

            // Cerca tutti i messaggi completi del tipo !...;
            while (true) {
                int start = _serialBuffer.IndexOf('!');
                int end = _serialBuffer.IndexOf(';', start + 1);

                if (start != -1 && end != -1 && end > start) {
                    var fullMessage = _serialBuffer.Substring(start, end - start + 1);
                    Console.WriteLine($"Messaggio completo: {fullMessage}");
                    MessageReceived?.Invoke($"RX: {fullMessage}");

                    // Elabora qui il messaggio
                    HandleSerialMessage(fullMessage);

                    // Rimuovi il messaggio dal buffer
                    _serialBuffer = _serialBuffer.Substring(end + 1);
                } else {
                    // Nessun messaggio completo, aspetta altri dati
                    break;
                }
            }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore nella ricezione: {ex.Message}");
                MessageReceived?.Invoke($"Errore RX: {ex.Message}");
            }
        }
    }

    private static string FormatSerialText(string text)
    {
        return text
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

    private async Task PollSerialInputAsync(int durationMs, int intervalMs = 100)
    {
        int elapsedMs = 0;
        while (_serialPort.IsOpen && elapsedMs < durationMs)
        {
            await Task.Delay(intervalMs);
            ReadAvailableSerialData();
            elapsedMs += intervalMs;
        }
    }

    private async void HandleSerialMessage(string message)
    {
        if (message.StartsWith("!STATUS") && message.EndsWith(';'))
        {
            var match = Regex.Match(message, @"!STATUS(\d);");
            if (match.Success)
            {
                int statusNumber = int.Parse(match.Groups[1].Value);
                switch (statusNumber)
                {
                    case 1:
                        LogProgress?.Invoke(0);
                        dataArray.Clear();
                        break;
                    case 2:
                        LogProgress?.Invoke(100);
                        FinishRead();
                        break;
                    case 3:
                        //LogProgress?.Invoke(0);
                        
                        break;
                    case 4:
                        LogProgress?.Invoke(100);
                        break;
                    case 5:
                        length += 60;
                        if (readPurpose != ReadPurpose.Instruction && totalLength > 0) LogProgress?.Invoke(length * 100 / totalLength);
                        break;
                    default:
                        Console.WriteLine("Stato sconosciuto");
                        break;
                }
            }
        }
        else if (message.StartsWith("!DATA") && message.EndsWith(';'))
        {
            string payload = message.Substring(5, message.Length - 6);
            if (!string.IsNullOrEmpty(payload))
            {
                try
                {
                    byte[] recBytes = Convert.FromBase64String(payload);

                    length += recBytes.Length;
                    if (readPurpose != ReadPurpose.Instruction && totalLength > 0) LogProgress?.Invoke(length * 100 / totalLength);

                    dataArray.AddRange(recBytes); // dataArray è List<byte>
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Errore nella decodifica Base64: " + ex.Message);
                }
            }
        }
        else if (message.StartsWith("!LOG") && message.EndsWith(';'))
        {
            string payload = message.Substring(4, message.Length - 5);
            if (!string.IsNullOrEmpty(payload))
            {
                try
                {
                    byte[] recBytes = Convert.FromBase64String(payload);
                    
                    if (recBytes.Length < 4) return;
                    var add = (ushort)((recBytes[0] << 8) + recBytes[1]);
                    var data = recBytes[2];
                    var control = recBytes[3];

                    var rd = (control & 0x20) == 0x20;
                    var wr = (control & 0x10) == 0x10;
                    var iorq = (control & 0x08) == 0x08;
                    var mreq = (control & 0x04) == 0x04;
                    var rfsh = (control & 0x02) == 0x02;
                    var m1 = (control & 0x01) == 0x01;

                    var injected = (control & 0x40) != 0;
                    var reset = (control & 0x80) != 0;
                    uint? clockNumber = recBytes.Length >= 9 ? ((uint)recBytes[5] << 24) | ((uint)recBytes[6] << 16) | ((uint)recBytes[7] << 8) | recBytes[8] : null;
                    byte? traceFlags = recBytes.Length >= 9 ? recBytes[4] : null;
                    var log = new LogElement(add, data, rd, wr, iorq, mreq, rfsh, m1, "", injected, reset, clockNumber, traceFlags);
                    if (log.IsInstructionFetch) instructionStartLog = log;

                    LogReceived?.Invoke(log);
                        
                    if (!injected && !reset && rfsh && !mreq && !rd)
                    {
                        string mnem = disassembler.Disassemble(add, data);
                        if (mnem != "" && instructionStartLog is not null)
                        { 
                            LogMnemonicReceived?.Invoke(instructionStartLog, mnem);
                            instructionStartLog = null;
                        } 
                    }
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Errore nella decodifica Base64: " + ex.Message);
                }
            }
        }
        // Attenzione all'ordine: "!DUMP24" soddisfa anche StartsWith("!DUMP"),
        // quindi il caso esteso va riconosciuto per primo.
        else if (message.StartsWith("!DUMP24") && message.EndsWith(';'))
        {
            string payload = message.Substring(7, message.Length - 8);
            if (!string.IsNullOrEmpty(payload))
            {
                try
                {
                    byte[] b = Convert.FromBase64String(payload);
                    if (b.Length < 30)
                    {
                        MessageReceived?.Invoke($"!DUMP24 troppo corto: {b.Length} byte");
                        return;
                    }

                    static uint U24(byte[] src, int at) =>
                        (uint)((src[at] << 16) | (src[at + 1] << 8) | src[at + 2]);

                    var registers = new Ez80Registers(
                        A: b[0], F: b[1], Mbase: b[2], Stat: b[3],
                        Bc: U24(b, 4), De: U24(b, 7), Hl: U24(b, 10),
                        Ix: U24(b, 13), Iy: U24(b, 16), Spl: U24(b, 19),
                        Sps: (ushort)((b[22] << 8) | b[23]),
                        Pc: U24(b, 24),
                        I: b[27], R: b[28], Iff2: b[29] != 0);

                    Dump24Received?.Invoke(registers);
                }
                catch (FormatException ex)
                {
                    Console.WriteLine("Errore nella decodifica Base64: " + ex.Message);
                }
            }
        }
        else if (message.StartsWith("!DUMP") && message.EndsWith(';'))
        {
            string payload = message.Substring(5, message.Length - 6);
            if (!string.IsNullOrEmpty(payload))
            {
                byte[] recBytes = Convert.FromBase64String(payload);

                var a_reg = recBytes[0];
                var b_reg = recBytes[1];
                var c_reg = recBytes[2];
                var d_reg = recBytes[3];
                var e_reg = recBytes[4];
                var h_reg = recBytes[5];
                var l_reg = recBytes[6];
                var ix_reg = (ushort)(recBytes[7]<<8 | recBytes[8]);
                var iy_reg = (ushort)(recBytes[9]<<8 | recBytes[10]);
                var sp_reg = (ushort)(recBytes[11]<<8 | recBytes[12]);
                var f_reg = recBytes[13];
                var pc_reg = (ushort)(recBytes[14]<<8 | recBytes[15]);

                DumpReceived?.Invoke(a_reg, b_reg, c_reg, d_reg, e_reg, h_reg, l_reg, ix_reg, iy_reg, sp_reg, pc_reg, f_reg);
            }
        }
        else if (message == "!OK;") 
        {
            MessageReceived?.Invoke("OK");
            //Console.WriteLine("Arduino ha risposto OK");
        }
        else if (message.StartsWith("!ERROR") && message.EndsWith(';'))
        {
            string payload = message.Substring(7, message.Length - 8);
            MessageReceived?.Invoke(payload);
            if (readPurpose == ReadPurpose.Instruction) FailInstructionRead(payload);
            else if (readPurpose == ReadPurpose.Memory) { readPurpose = ReadPurpose.None; ReadStateChanged?.Invoke(); }
            //Console.WriteLine(payload);
        } else if (message.StartsWith("!INFO") && message.EndsWith(';'))
        {
            string payload = message.Substring(6, message.Length - 7);
            var config = CardConfig.Parse(payload);
            MessageReceived?.Invoke(
                $"INFO parsed: CARD={config.CardName}, SCLK={config.IsClockFromArduino}, " +
                $"FCLK={config.ClockFrequency}, ECLK={config.ClockActive}, SMEM={config.IsMemFromArduino}");
            ConfigReceived?.Invoke(config);
            Console.WriteLine(payload);
        }
    }
    
    public async Task<bool> OpenCloseSerialPortAsync(string portName, int baudRate) {
          if (_serialPort.IsOpen) {
              _serialPort.Close();
              ClearPendingRead();
              mainViewModel.IsSerialOpened = false;
          } else {
              _serialPort.PortName = portName;
              _serialPort.BaudRate = baudRate;
             lock (_serialReadLock) { _serialBuffer = string.Empty; }
             _serialPort.Open();
             mainViewModel.IsSerialOpened = true;
             
             await Task.Delay(2000); // Aspetta 2 secondi
             SetComputerConnected();

             await Task.Delay(400);  // Aspetta altri 0.4 secondi
             RefreshConfiguration();

             // Su alcune porte USB native (ad esempio UNO R4) l'evento
             // DataReceived può non essere notificato immediatamente.
             await PollSerialInputAsync(2000);

             // Ora l'interfaccia aspetta !INFO per accendere i comandi che vogliono una
             // scheda, quindi una risposta persa non è più solo un badge vuoto: lascia
             // l'IDE inutilizzabile. La richiesta si ripete un paio di volte, perché a
             // volte la scheda si sta ancora riavviando quando arriva la prima.
             for (var attempt = 0; attempt < 2 && _serialPort.IsOpen && !mainViewModel.IsBoardConnected; attempt++)
             {
                 RefreshConfiguration();
                 await PollSerialInputAsync(1500);
             }
          }

          return _serialPort.IsOpen;
    }

    public bool CloseSerialPort()
    {
        _serialPort.Close();
        ClearPendingRead();
        mainViewModel.IsSerialOpened = false;
        return _serialPort.IsOpen;
    }
    
    public void SendSerialText(string text) {
        if (_serialPort.IsOpen) {
            MessageReceived?.Invoke($"TX: {text.TrimEnd('\r', '\n')}");
            _serialPort.Write(text); // Scrive la stringa direttamente
        } else {
            Console.WriteLine("Porta seriale non aperta.");
        }
    }
    
    /// <summary>
    /// L'indirizzo viaggia in esadecimale per Z80, in decimale per eZ80;
    /// la lunghezza e' sempre decimale. Sono uint e non ushort perche' la
    /// RAM dell'eZ80 sta a 0xFFF000, fuori dai 64 KB indirizzabili da uno Z80.
    /// </summary>
    public void ReadData(uint start, uint size) {
        if (IsReadPending) { MessageReceived?.Invoke("Una lettura è già in corso."); return; }
        discardLateInstructionResponse = false;
        readPurpose = ReadPurpose.Memory; length = 0; ReadStateChanged?.Invoke();
        totalLength = (int)size;   // il contatore di avanzamento resta int
        
        SendSerialText($"rb {FormatMemoryAddress(start, mainViewModel.IsEz80)} {size}\r");
    }

    // Il formato dipende dal firmware collegato, non dalla CPU del progetto
    // o dalla modalita' ADL/Z80 selezionata per l'eZ80.
    public static string FormatMemoryAddress(uint address, bool isEz80) =>
        address.ToString(isEz80 ? "D" : "X", System.Globalization.CultureInfo.InvariantCulture);
        
    public async void WriteData(byte[] data, uint startAddress)
    {
        if (IsReadPending) { MessageReceived?.Invoke("Attendere la lettura in corso."); return; }
        var startHex = FormatMemoryAddress(startAddress, mainViewModel.IsEz80);
        
        writeDataArray = new List<byte>(data);
        
        SendSerialText($"wb {startHex}\r");

        try
        {
            _serialPort.DataReceived -= DataReceivedHandler;

            Console.WriteLine("Inizio invio");

            var xmodem = new XModemSender(SerialPort);

            xmodem.ProgressPercentageChanged += percent =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    mainViewModel.ProgressValue = (int)percent;
                    mainViewModel.ProgressVisible = percent < 100.0;

                    if (percent >= 100.0)
                    {
                        mainViewModel.DataTable.Clear();
                    }
                    
                });
            };

            await xmodem.SendAsync(writeDataArray);
            await DrainInputAsync(quietMs: 250, maxMs: 2000, ct: CancellationToken.None);
            // opzionale ma utile per riallineare
            await SendSyncNewlinesAsync(CancellationToken.None);
            await DrainInputAsync(quietMs: 250, maxMs: 1500, ct: CancellationToken.None);

            Console.WriteLine("Trasferimento completato.");
            
            
            
            _serialPort.DataReceived += DataReceivedHandler;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Errore {ex.Message}");
            mainViewModel.ProgressVisible = false;
        }
        
        

        /*totalLength = writeDataArray.Count;
      
        Console.WriteLine("Scrivo byte: " + totalLength);
        
        SendSerialText($"wbf {startHex} {totalLength}\r");
        
        if (_serialPort.IsOpen)
        {
            // Invia blocchi base64
            const int chunkSize = 48; // = 64 char base64, buona dimensione
            for (int i = 0; i < totalLength; i += chunkSize)
            {
                int len = Math.Min(chunkSize, totalLength - i);
                string base64Chunk = Convert.ToBase64String(data, i, len);
                Console.WriteLine("base64Chunk: " + base64Chunk);
                _serialPort.WriteLine(base64Chunk);

                await Task.Delay(100); // piccola pausa per non saturare
            }
        }
        else
        {
            Console.WriteLine("Porta seriale non aperta.");
        }*/
        
        
        /*

        // 1. Invia comando iniziale
        //SendSerialText($"wb {startHex} {totalLength}\r");
        

        // 2. Invia i dati in chunk base64 da 64 byte
        if (_serialPort.IsOpen)
        {
            int chunkSize = 64;

            for (int i = 0; i < bytesToSend.Count; i += chunkSize)
            {
                await Task.Delay(1000); // aspetta 1 secondo tra i blocchi
                
                int size = Math.Min(chunkSize, bytesToSend.Count - i);
                byte[] chunk = bytesToSend.GetRange(i, size).ToArray();

                _serialPort.Write(chunk, 0, chunk.Length); // invio diretto dei byte
            }
        }
        else
        {
            Console.WriteLine("Porta seriale non aperta.");
        }*/
       
        /*LogProgress?.Invoke(0);

        writeDataArray = new List<byte>(data);
        
        totalLength = writeDataArray.Count;
        currentAddress = startAddress;
        chunkNum = 0;

        int length = totalLength;
        dataFinished = length <= 50;
        
        if (length > 50)
        {
            length = 50;
        }

        //WriteData((UInt16)currentAddress, (UInt16)length);
        SendSerialText($"wb {startAddress} {totalLength}\r");
        
        if (_serialPort != null && _serialPort.IsOpen)
        {
            int chunkSize = 64;

            for (int i = 0; i < totalLength; i += chunkSize)
            {
                int remaining = totalLength - i;
                int currentChunkSize = Math.Min(chunkSize, remaining);

                byte[] chunk = new byte[currentChunkSize];
                Array.Copy(bytesToSend, i, chunk, 0, currentChunkSize);

                _serialPort.Write(chunk, 0, chunk.Length);

                await Task.Delay(1000); // Aspetta 1 secondo tra i chunk
            }
        }
        else
        {
            Console.WriteLine("Porta seriale non aperta.");
        }*/
    }

    public void RestartBoard()
    {
        disassembler.Restart();
        instructionStartLog = null;
    }
    
    private void SetComputerConnected()
    { 
        SendSerialText("cc\r");
    }

    /// <summary>Ferma la CPU in break ZDI. Solo scheda eZ80.</summary>
    public void BreakCpu() => SendSerialText("break\r");

    /// <summary>
    /// Carica il PC dell'eZ80 e fa ripartire la CPU da li'. L'indirizzo viaggia
    /// in decimale, come per rb e wb sul firmware eZ80.
    /// </summary>
    public void RunFrom(uint address) => SendSerialText($"go {address}\r");

    /// <summary>Fa ripartire la CPU dopo un break. Solo scheda eZ80.</summary>
    public void ContinueCpu() => SendSerialText("cont\r");

    /// <summary>
    /// Sceglie la modalita' con cui il firmware accede alla memoria: ADL
    /// (indirizzi a 24 bit) oppure Z80 (MBASE:PC, 64 KB). Solo scheda eZ80.
    /// </summary>
    public void SetAdlMode(bool adl) => SendSerialText($"adl {(adl ? 1 : 0)}\r");
    
    /// <summary>Read actual board state without changing its settings.</summary>
    public void RefreshConfiguration()
    { 
        SendSerialText("info\r");
    }
    
    public void SetClockFromArduino()
    {
        SendSerialText("clk arduino\r");
        RefreshConfiguration();
    }
    
    public void SetClockExternal()
    {
        SendSerialText("clk external\r");
        RefreshConfiguration();
    }
    
    public void SetMemoryFromArduino()
    {
        SendSerialText("mem arduino\r");
        RefreshConfiguration();
    }
    
    public void SetMemoryExternal()
    {
        SendSerialText("mem external\r");
        RefreshConfiguration();
    }
    
    public async Task DrainInputAsync(int quietMs = 200, int maxMs = 2000, CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var quiet = System.Diagnostics.Stopwatch.StartNew();
        var buf = new byte[256];

        while (sw.ElapsedMilliseconds < maxMs && !ct.IsCancellationRequested)
        {
            int available = _serialPort.BytesToRead;
            if (available > 0)
            {
                int toRead = Math.Min(available, buf.Length);
                // usa BaseStream async per non bloccare
                int read = await _serialPort.BaseStream.ReadAsync(buf.AsMemory(0, toRead), ct).ConfigureAwait(false);
                if (read > 0) quiet.Restart(); // abbiamo consumato roba: ricomincia il timer di “silenzio”
            }
            else
            {
                if (quiet.ElapsedMilliseconds >= quietMs) break; // silenzio stabile
                await Task.Delay(30, ct).ConfigureAwait(false);
            }
        }
    }
    
    public async Task SendSyncNewlinesAsync(CancellationToken ct = default)
    {
        var nl = new byte[] { (byte)'\r', (byte)'\n' };
        await _serialPort.BaseStream.WriteAsync(nl, ct).ConfigureAwait(false);
        await _serialPort.BaseStream.FlushAsync(ct).ConfigureAwait(false);
    }
}
