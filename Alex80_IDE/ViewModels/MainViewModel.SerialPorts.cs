using System;
using System.Linq;
using Alex80_IDE.Models;

public partial class MainViewModel
{
    private readonly SerialPortSelectionState _serialPortSelection;
    private bool _isRefreshingSerialPorts;
    private bool _isRestoringSerialBaudRate;
    private string? _baudRatePort;

    partial void OnSelectedIdxChanged(int value)
    {
        if (!_isRefreshingSerialPorts && value >= 0 && value < PortsList.Count)
        {
            _serialPortSelection.SelectManually(PortsList[value]);
            RestoreSelectedPortBaudRate();
        }
    }

    private void ApplyAvailableSerialPorts(string[] availablePorts)
    {
        if (availablePorts.SequenceEqual(PortsList)) return;

        var openPortName = _serialManager.SerialPort.IsOpen ? _serialManager.SerialPort.PortName : null;
        if (openPortName is not null && !availablePorts.Contains(openPortName))
        {
            _serialManager.CloseSerialPort();
            openPortName = null;
        }

        // Clear e aggiornamenti della ComboBox possono cambiare temporaneamente
        // SelectedIdx: non devono diventare scelte manuali né modificare la cronologia.
        _isRefreshingSerialPorts = true;
        try
        {
            PortsList.Clear();
            foreach (var port in availablePorts) PortsList.Add(port);
            SelectedIdx = _serialPortSelection.ChooseIndex(availablePorts, openPortName);
        }
        finally { _isRefreshingSerialPorts = false; }
        RestoreSelectedPortBaudRate();
    }

    private void RestoreSelectedPortBaudRate()
    {
        if (IsSerialOpened) return;
        var port = SelectedIdx >= 0 && SelectedIdx < PortsList.Count ? PortsList[SelectedIdx] : null;
        if (port == _baudRatePort) return;
        _baudRatePort = port;
        if (port is null || _serialPortSelection.GetRememberedBaudRate(port) is not { } baudRate || !BaudRates.Contains(baudRate)) return;

        // Una scansione con la stessa porta selezionata non deve cancellare una
        // velocità appena scelta dall'utente per il prossimo tentativo.
        _isRestoringSerialBaudRate = true;
        try { SelectedBaudRate = baudRate; }
        finally { _isRestoringSerialBaudRate = false; }
    }

    private void RememberIdentifiedSerialPort()
    {
        if (IsSerialOpened && (IsAlex80Board || IsEz80) && _serialManager.SerialPort.IsOpen)
            _serialPortSelection.RememberConnectedPort(_serialManager.SerialPort.PortName, _serialManager.SerialPort.BaudRate);
    }
}
