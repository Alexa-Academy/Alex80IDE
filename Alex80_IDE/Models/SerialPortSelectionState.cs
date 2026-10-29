using System;
using System.Collections.Generic;
using System.Linq;

namespace Alex80_IDE.Models;

/// <summary>Preferenze di porta: si ricordano solo connessioni identificate, senza aprire porte.</summary>
public sealed class SerialPortSelectionState
{
    private const int HistoryLimit = 10;
    private readonly List<string> _recentPorts;
    private readonly Dictionary<string, int> _baudRates;
    private readonly Action<IReadOnlyList<string>, IReadOnlyDictionary<string, int>> _saveHistory;
    private string? _manualPort;
    private string? _selectedPort;

    public SerialPortSelectionState(IEnumerable<string>? recentPorts, IReadOnlyDictionary<string, int>? baudRates,
        Action<IReadOnlyList<string>, IReadOnlyDictionary<string, int>> saveHistory)
    {
        _recentPorts = (recentPorts ?? Array.Empty<string>()).Where(port => !string.IsNullOrWhiteSpace(port))
            .Distinct(StringComparer.Ordinal).Take(HistoryLimit).ToList();
        _baudRates = (baudRates ?? new Dictionary<string, int>())
            .Where(pair => _recentPorts.Contains(pair.Key) && pair.Value > 0)
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        _saveHistory = saveHistory;
    }

    public void SelectManually(string port) => _manualPort = port;

    public int ChooseIndex(string[] availablePorts, string? openPort)
    {
        // La porta aperta non cambia durante una sessione. Una scelta esplicita
        // prevale sulla cronologia, anche quando arriva un'altra scheda conosciuta.
        var candidates = new[] { openPort, _manualPort }.Concat(_recentPorts).Append(_selectedPort);
        _selectedPort = candidates.FirstOrDefault(port => port is not null && Array.IndexOf(availablePorts, port) >= 0)
            ?? availablePorts.FirstOrDefault();
        return _selectedPort is null ? -1 : Array.IndexOf(availablePorts, _selectedPort);
    }

    public int? GetRememberedBaudRate(string port) => _baudRates.TryGetValue(port, out var baudRate) ? baudRate : null;

    public void RememberConnectedPort(string port, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(port) || baudRate <= 0) return;
        _manualPort = port;
        if (_recentPorts.Count > 0 && _recentPorts[0] == port && GetRememberedBaudRate(port) == baudRate) return;
        _baudRates[port] = baudRate;
        _recentPorts.Remove(port);
        _recentPorts.Insert(0, port);
        if (_recentPorts.Count > HistoryLimit)
        {
            _baudRates.Remove(_recentPorts[HistoryLimit]);
            _recentPorts.RemoveAt(HistoryLimit);
        }
        _saveHistory(_recentPorts.ToArray(), new Dictionary<string, int>(_baudRates));
    }
}
