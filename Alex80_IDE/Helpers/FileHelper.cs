using System;
using System.IO;

public class FileHelper
{
    public static byte[]? GetFile(string filePath)
    {
        try
        {
            // Legge il contenuto del file come array di byte
            return File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            // Gestione dell'errore (esempio: file non trovato)
            Console.WriteLine($"Errore durante la lettura del file: {ex.Message}");
            return null;
        }
    }
}