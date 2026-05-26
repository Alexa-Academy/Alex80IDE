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

    private int logId;
    private int idxM1Start;
    private bool dataFinished;

    private byte command;
    private readonly List<byte> dataArray;
    private List<byte> writeDataArray;
 //   private readonly List<byte> tmpDataArray;
    
    public event Action<byte[]>? DataReceived;
    public event Action<LogElement>? LogReceived; 
    public event Action<int>? LogProgress;
    public event Action<int, string>? LogMnemonicReceived; 
    public event Action<byte, byte, byte, byte, byte, byte, byte, ushort, ushort, ushort, ushort, byte>? DumpReceived; 
    public event Action<string>? MessageReceived;
    public event Action<CardConfig>? ConfigReceived;
   
    private string _serialBuffer = "";

    private int totalLength;
    private int length;
    
    public SerialManager(MainViewModel mainViewModel)
    {
        dataArray = [];
       // tmpDataArray = [];
        
        this.mainViewModel = mainViewModel;

        mainViewModel.IsSerialOpened = false;
        
        _serialPort = new SerialPort {
              BaudRate = 19200,           // Velocità di trasmissione
              Parity = Parity.None,       // Parità
              DataBits = 8,               // Bit di dati
              StopBits = StopBits.One,    // Bit di stop
              Handshake = Handshake.None  // Handshake
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
        if (!_serialPort.IsOpen) return;
        
        try {
            string incoming = _serialPort.ReadExisting();
            _serialBuffer += incoming;

            // Cerca tutti i messaggi completi del tipo !...;
            while (true) {
                int start = _serialBuffer.IndexOf('!');
                int end = _serialBuffer.IndexOf(';', start + 1);

                if (start != -1 && end != -1 && end > start) {
                    var fullMessage = _serialBuffer.Substring(start, end - start + 1);
                    Console.WriteLine($"Messaggio completo: {fullMessage}");

                    // Elabora qui il messaggio
                    HandleSerialMessage(fullMessage);

                    // Rimuovi il messaggio dal buffer
                    _serialBuffer = _serialBuffer.Substring(end + 1);
                } else {
                    // Nessun messaggio completo, aspetta altri dati
                    break;
                }
            }

        } catch (Exception ex) {
            Console.WriteLine($"Errore nella ricezione: {ex.Message}");
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
                        DataReceived?.Invoke(dataArray.ToArray());
                        break;
                    case 3:
                        //LogProgress?.Invoke(0);
                        
                        break;
                    case 4:
                        LogProgress?.Invoke(100);
                        break;
                    case 5:
                        length += 60;
                        LogProgress?.Invoke(length * 100 / totalLength);
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
                    LogProgress?.Invoke(length * 100 / totalLength);

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
                    
                    var add = (ushort)((recBytes[0] << 8) + recBytes[1]);
                    var data = recBytes[2];
                    var control = recBytes[3];

                    var rd = (control & 0x20) == 0x20;
                    var wr = (control & 0x10) == 0x10;
                    var iorq = (control & 0x08) == 0x08;
                    var mreq = (control & 0x04) == 0x04;
                    var rfsh = (control & 0x02) == 0x02;
                    var m1 = (control & 0x01) == 0x01;

                    if (!m1 && !mreq && !rd) {
                        idxM1Start = logId;
                    }
                        
                    LogReceived?.Invoke(new LogElement(add, data, rd, wr, iorq, mreq, rfsh, m1, ""));
                        
                    logId++;
                        
                    if (rfsh && !mreq && !rd)
                    {
                        string mnem = disassembler.Disassemble(add, data);
                        if (mnem != "")
                        { 
                            LogMnemonicReceived?.Invoke(idxM1Start, mnem);
                        } 
                    }
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
            //Console.WriteLine(payload);
        } else if (message.StartsWith("!INFO") && message.EndsWith(';'))
        {
            string payload = message.Substring(6, message.Length - 7);
            ConfigReceived?.Invoke(CardConfig.Parse(payload));
            Console.WriteLine(payload);
        }
    }
    
    public async Task<bool> OpenCloseSerialPortAsync(string portName) {
          if (_serialPort.IsOpen) {
              _serialPort.Close();
              mainViewModel.IsSerialOpened = false;
          } else {
              _serialPort.PortName = portName;
             _serialPort.Open();
             mainViewModel.IsSerialOpened = true;
             
             await Task.Delay(2000); // Aspetta 2 secondi
             SetComputerConnected();

             await Task.Delay(400);  // Aspetta altri 0.4 secondi
             GetInfo();
          }

          return _serialPort.IsOpen;
    }

    public bool CloseSerialPort()
    {
        _serialPort.Close();
        mainViewModel.IsSerialOpened = false;
        return _serialPort.IsOpen;
    }
    
    public void SendSerialText(string text) {
        if (_serialPort.IsOpen) {
            _serialPort.Write(text); // Scrive la stringa direttamente
        } else {
            Console.WriteLine("Porta seriale non aperta.");
        }
    }
    
    public void ReadData(ushort start, ushort size) {
        totalLength = size;
        
        SendSerialText($"rb {start} {size}\r");
    }
        
    public async void WriteData(byte[] data, ushort startAddress)
    {
        var startHex = startAddress.ToString();
        
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
        idxM1Start = 0;
        logId = 0;
    }
    
    private void SetComputerConnected()
    { 
        SendSerialText("cc\r");
    }
    
    private void GetInfo()
    { 
        SendSerialText("info\r");
    }
    
    public void SetClockFromArduino()
    {
        SendSerialText("clk arduino\r");
    }
    
    public void SetClockExternal()
    {
        SendSerialText("clk external\r");
    }
    
    public void SetMemoryFromArduino()
    {
        SendSerialText("mem arduino\r");
    }
    
    public void SetMemoryExternal()
    {
        SendSerialText("mem external\r");
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