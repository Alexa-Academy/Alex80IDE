using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace Alex80_IDE.Services
{
     /// <summary>
    /// Implements basic XModem protocol (checksum-based) for sending data over a serial port.
    /// Provides progress notifications by block count and percentage.
    /// </summary>
    public class XModemSender
    {
        private readonly SerialPort _serialPort;
        private const byte SOH = 0x01;       // Start of 128-byte data packet
        private const byte EOT = 0x04;       // End of transmission
        private const byte ACK = 0x06;       // Acknowledge
        private const byte NAK = 0x15;       // Negative acknowledge
        private const byte CAN = 0x18;       // Cancel
        private const byte CPMEOF = 0x1A;    // Padding (Ctrl-Z)
        private const int PacketSize = 128;
        private const int MaxRetries = 10;

        /// <summary>
        /// Raised when a block is successfully transmitted. Args: (sentBlocks, totalBlocks).
        /// </summary>
        public event Action<int, int> ProgressChanged;

        /// <summary>
        /// Raised when the percentage of transmission completion changes. Args: percentage (0.0–100.0).
        /// </summary>
        public event Action<double> ProgressPercentageChanged;

        public XModemSender(SerialPort serialPort)
        {
            _serialPort = serialPort ?? throw new ArgumentNullException(nameof(serialPort));
            if (!_serialPort.IsOpen)
                throw new InvalidOperationException("Serial port must be open before sending.");
        }

        /// <summary>
        /// Sends the given data list via XModem protocol.
        /// </summary>
        /// <param name="writeDataArray">Data to send.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /*public async Task SendAsync(List<byte> writeDataArray, CancellationToken cancellationToken = default)
        {
            if (writeDataArray == null)
                throw new ArgumentNullException(nameof(writeDataArray));

            int totalBlocks = (writeDataArray.Count + PacketSize - 1) / PacketSize;
            int offset = 0;

            // Wait for receiver to send NAK to start
            while (true)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException();
                if (_serialPort.BytesToRead > 0)
                {
                    int incoming = _serialPort.ReadByte();
                    if (incoming == NAK) break;
                }
                await Task.Delay(100, cancellationToken);
            }

            // Send all blocks
            for (int blockNumber = 1; blockNumber <= totalBlocks; blockNumber++)
            {
                byte[] packet = new byte[PacketSize];
                int bytesToCopy = Math.Min(PacketSize, writeDataArray.Count - offset);
                writeDataArray.CopyTo(offset, packet, 0, bytesToCopy);
                if (bytesToCopy < PacketSize)
                {
                    // pad with CPMEOF
                    for (int i = bytesToCopy; i < PacketSize; i++) packet[i] = CPMEOF;
                }

                int retries = 0;
                bool success = false;
                while (retries < MaxRetries && !success)
                {
                    if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException();

                    // Build and send packet
                    _serialPort.Write(new[] { SOH }, 0, 1);
                    _serialPort.Write(new[] { (byte)blockNumber, (byte)(255 - blockNumber) }, 0, 2);
                    _serialPort.Write(packet, 0, PacketSize);
                    byte checksum = CalculateChecksum(packet);
                    _serialPort.Write(new[] { checksum }, 0, 1);

                    // Wait for ACK/NAK/CAN
                    int response = _serialPort.ReadByte();
                    if (response == ACK)
                    {
                        success = true;
                        ProgressChanged?.Invoke(blockNumber, totalBlocks);
                        double percent = (double)blockNumber / totalBlocks * 100.0;
                        
                        ProgressPercentageChanged?.Invoke(percent);
                    }
                    else if (response == NAK)
                    {
                        retries++;
                    }
                    else if (response == CAN)
                    {
                        throw new IOException("Transfer canceled by receiver.");
                    }
                }

                if (!success)
                    throw new IOException($"Failed to transmit block {blockNumber} after {MaxRetries} retries.");

                offset += PacketSize;
            }

            // Send EOT
            int eotRetries = 0;
            while (eotRetries < MaxRetries)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException();
                _serialPort.Write(new[] { EOT }, 0, 1);
                int response = _serialPort.ReadByte();
                if (response == ACK) break;
                eotRetries++;
            }

            if (eotRetries >= MaxRetries)
                throw new IOException("No ACK after EOT.");
        }*/
        


    public async Task SendAsync(List<byte> writeDataArray, CancellationToken cancellationToken = default)
    {
        if (writeDataArray is null) throw new ArgumentNullException(nameof(writeDataArray));
        if (!_serialPort.IsOpen) throw new IOException("Serial port is not open.");

        // Parametri timeout (regolabili)
        const int initialNakTimeoutMs = 15000; // attesa massima NAK iniziale
        const int ackNakTimeoutMs     = 5000;  // attesa ACK/NAK per ogni pacchetto
        const int eotAckTimeoutMs     = 5000;  // attesa ACK dopo EOT

        // Helpers locali ---------------

        async Task WriteAsync(byte[] buffer, int offset, int count)
            => await _serialPort.BaseStream.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).ConfigureAwait(false);

        async Task WriteByteAsync(byte b)
        {
            var one = new[] { b };
            await _serialPort.BaseStream.WriteAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
        }

        async Task FlushAsync()
            => await _serialPort.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

        // Legge un byte con timeout; ritorna null se scade il tempo
        async Task<byte?> ReadByteAsync(int timeoutMs)
        {
            var buf = new byte[1];
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            try
            {
                int read = await _serialPort.BaseStream.ReadAsync(buf.AsMemory(0, 1), cts.Token).ConfigureAwait(false);
                return read == 1 ? buf[0] : (byte?)null;
            }
            catch (OperationCanceledException) { return null; }
        }

        // Attende uno tra più byte attesi, entro un timeout. Ritorna il byte o null se timeout.
        async Task<byte?> WaitForOneOfAsync(int timeoutMs, params byte[] expected)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs && !cancellationToken.IsCancellationRequested)
            {
                // se ci sono dati pronti, prova a leggerli
                if (_serialPort.BytesToRead > 0)
                {
                    var b = await ReadByteAsync(timeoutMs: 250).ConfigureAwait(false);
                    if (b.HasValue && expected.Contains(b.Value)) return b.Value;
                }
                else
                {
                    // Evita busy-wait
                    await Task.Delay(50, cancellationToken).ConfigureAwait(false);
                }
            }
            return null;
        }

        // --------------------------------

        int totalBlocks = (writeDataArray.Count + PacketSize - 1) / PacketSize;
        int offset = 0;

        // 1) Attesa NAK iniziale dal ricevitore
        {
            var b = await WaitForOneOfAsync(initialNakTimeoutMs, NAK).ConfigureAwait(false);
            if (b != NAK)
                throw new IOException("XMODEM start failed: no initial NAK from receiver.");
        }

        // 2) Invio di tutti i blocchi
        for (int blockNumber = 1; blockNumber <= totalBlocks; blockNumber++)
        {
            // Prepara il pacchetto (128 bytes, padding CPMEOF)
            var packet = new byte[PacketSize];
            int bytesToCopy = Math.Min(PacketSize, writeDataArray.Count - offset);
            writeDataArray.CopyTo(offset, packet, 0, bytesToCopy);
            if (bytesToCopy < PacketSize)
                Array.Fill(packet, CPMEOF, bytesToCopy, PacketSize - bytesToCopy);

            int retries = 0;
            bool success = false;

            while (retries < MaxRetries && !success)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // SOH + blk + ~blk + payload + checksum
                byte blk = (byte)blockNumber;
                byte nblk = (byte)(255 - blk);
                byte checksum = CalculateChecksum(packet);

                await WriteByteAsync(SOH).ConfigureAwait(false);
                await WriteAsync(new[] { blk, nblk }, 0, 2).ConfigureAwait(false);
                await WriteAsync(packet, 0, PacketSize).ConfigureAwait(false);
                await WriteByteAsync(checksum).ConfigureAwait(false);
                await FlushAsync().ConfigureAwait(false);

                // Attesa ACK / NAK / CAN
                var resp = await WaitForOneOfAsync(ackNakTimeoutMs, ACK, NAK, CAN).ConfigureAwait(false);
                if (resp == ACK)
                {
                    success = true;

                    // Notifiche progresso (NON toccano UI qui)
                    ProgressChanged?.Invoke(blockNumber, totalBlocks);
                    double percent = (double)blockNumber / totalBlocks * 100.0;
                    ProgressPercentageChanged?.Invoke(percent);
                }
                else if (resp == NAK)
                {
                    retries++;
                    // ritenta
                }
                else if (resp == CAN)
                {
                    throw new IOException("Transfer canceled by receiver.");
                }
                else
                {
                    // timeout o byte inatteso
                    retries++;
                }
            }

            if (!success)
                throw new IOException($"Failed to transmit block {blockNumber} after {MaxRetries} retries.");

            offset += PacketSize;
        }

        // 3) Chiusura con EOT: gestisci NAK poi ACK
        {
            // primo EOT
            await WriteByteAsync(EOT).ConfigureAwait(false);
            await FlushAsync().ConfigureAwait(false);

            var resp1 = await WaitForOneOfAsync(eotAckTimeoutMs, ACK, NAK, CAN).ConfigureAwait(false);
            if (resp1 == CAN) throw new IOException("Transfer canceled by receiver at EOT.");

            if (resp1 == ACK)
            {
                // fine OK
            }
            else if (resp1 == NAK)
            {
                // secondo EOT richiesto da molti ricevitori
                await WriteByteAsync(EOT).ConfigureAwait(false);
                await FlushAsync().ConfigureAwait(false);

                var resp2 = await WaitForOneOfAsync(eotAckTimeoutMs, ACK, CAN).ConfigureAwait(false);
                if (resp2 == ACK) { /* fine OK */ }
                else if (resp2 == CAN) throw new IOException("Transfer canceled by receiver at EOT (second stage).");
                else throw new IOException("No ACK after second EOT.");
            }
            else
            {
                throw new IOException("Unexpected/no response after EOT.");
            }
        }
    }

        /// <summary>
        /// Calculates simple checksum (sum of bytes modulo 256).
        /// </summary>
        private static byte CalculateChecksum(byte[] data)
        {
            int sum = 0;
            foreach (var b in data) sum = (sum + b) & 0xFF;
            return (byte)sum;
        }
    }
}