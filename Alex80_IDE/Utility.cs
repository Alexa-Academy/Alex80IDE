using System;
using System.Globalization;
using System.IO.Ports;
using Avalonia.Data.Converters;

public static class Constants
{
    public const byte CMD_LOG = 0x00;
    public const byte CMD_READ_MEM = 0x40;
    public const byte CMD_WRITE_MEM = 0x41;
    public const byte CMD_RESTART = 0x42;
    public const byte CMD_STOP = 0x43;
    public const byte CMD_EN_LOG = 0x44;
    public const byte CMD_SEND_NMI = 0x46;
}

public static class Z80Formatter
{
    public static string ToZ80Hex(this int value)
    {
        // Se il valore è un byte (8 bit)
        if (value <= 0xFF)
        {
            string hexValue = value.ToString("X2");  // Formato esadecimale a 2 caratteri
            if (hexValue.StartsWith("A") || hexValue.StartsWith("B") || hexValue.StartsWith("C") ||
                hexValue.StartsWith("D") || hexValue.StartsWith("E") || hexValue.StartsWith("F"))
            {
                hexValue = "0" + hexValue;  // Aggiungiamo uno zero se inizia con A-F
            }
            return hexValue + "h";  // Aggiungiamo 'h' alla fine
        }
        // Se il valore è una word (16 bit)
        else
        {
            string hexValue = value.ToString("X4");  // Formato esadecimale a 4 caratteri
            if (hexValue.StartsWith("A") || hexValue.StartsWith("B") || hexValue.StartsWith("C") ||
                hexValue.StartsWith("D") || hexValue.StartsWith("E") || hexValue.StartsWith("F"))
            {
                hexValue = "0" + hexValue;  // Aggiungiamo uno zero se inizia con A-F
            }
            return hexValue + "h";  // Aggiungiamo 'h' alla fine
        }
    }
    
    // Gestisce il formato per 1 byte
    public static string ToZ80Hex(this byte value)
    {
        string hexValue = value.ToString("X2");  // Formato esadecimale a 2 caratteri
        if (hexValue.StartsWith("A") || hexValue.StartsWith("B") || hexValue.StartsWith("C") ||
            hexValue.StartsWith("D") || hexValue.StartsWith("E") || hexValue.StartsWith("F"))
        {
            hexValue = "0" + hexValue;  // Aggiungiamo uno zero se inizia con A-F
        }
        return hexValue + "h";  // Aggiungiamo 'h' alla fine
    }

    // Gestisce il formato per 2 byte (ushort)
    public static string ToZ80Hex(this ushort value)
    {
        string hexValue = value.ToString("X4");  // Formato esadecimale a 4 caratteri
        if (hexValue.StartsWith("A") || hexValue.StartsWith("B") || hexValue.StartsWith("C") ||
            hexValue.StartsWith("D") || hexValue.StartsWith("E") || hexValue.StartsWith("F"))
        {
            hexValue = "0" + hexValue;  // Aggiungiamo uno zero se inizia con A-F
        }
        return hexValue + "h";  // Aggiungiamo 'h' alla fine
    }
}

public static class SerialPortHelper
{
    public static string[] GetSerialPorts()
    {
        string[] allPorts = SerialPort.GetPortNames();

        if (OperatingSystem.IsMacOS())
        {
            // Su macOS filtra solo le porte `cu`
            return Array.FindAll(allPorts, port => port.StartsWith("/dev/cu."));
        }

        // Su Windows e Linux restituisci tutte le porte
        return allPorts;
    }
}


