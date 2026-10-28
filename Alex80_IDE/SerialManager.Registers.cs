using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alex80_IDE;

public partial class SerialManager
{
    private bool registerRequestPending;
    private CancellationTokenSource? registerRequestTimeout;
    public bool IsRegisterRequestPending => registerRequestPending;
    public event Action<string>? RegisterRequestFailed;

    public bool RequestRegisters(bool step = false, bool readAfterStep = true)
    {
        if (!_serialPort.IsOpen || IsReadPending || clockPulsePending || registerRequestPending) return false;
        registerRequestPending = true;
        ReadStateChanged?.Invoke();
        registerRequestTimeout = new CancellationTokenSource();
        _ = AwaitRegistersAsync(registerRequestTimeout.Token);
        SendSerialText(step
            ? (!readAfterStep && mainViewModel.HasRegisterUpdateSupport ? "s noregs\r" : "s\r")
            : "d\r");
        return true;
    }

    // At low clock rates the injected instructions take seconds to complete.
    // Poll USB as well as accepting DataReceived, using the same locked RX path.
    private int RegisterResponseTimeoutMs => mainViewModel.IsEz80 ? 10000 :
        10000 + (int)Math.Ceiling(512000.0 /
            (int.TryParse(mainViewModel.ClockFrequency, out var hz) && hz > 0 ? hz : 1));

    private async Task AwaitRegistersAsync(CancellationToken token)
    {
        int timeout = RegisterResponseTimeoutMs;
        try
        {
            for (int elapsed = 0; elapsed < timeout; elapsed += 100)
            {
                await Task.Delay(100, token);
                if (token.IsCancellationRequested || !registerRequestPending || !_serialPort.IsOpen) return;
                ReadAvailableSerialData();
            }
        }
        catch (OperationCanceledException) { return; }
        if (!token.IsCancellationRequested && registerRequestPending)
            CompleteRegisterRequest("Timeout nell'attesa dei registri o della fine dello step. Verifica la scheda prima di proseguire.");
    }

    private void CompleteRegisterRequest(string? error = null)
    {
        registerRequestPending = false;
        registerRequestTimeout?.Cancel();
        registerRequestTimeout?.Dispose();
        registerRequestTimeout = null;
        ReadStateChanged?.Invoke();
        if (error is not null) RegisterRequestFailed?.Invoke(error);
    }
}
