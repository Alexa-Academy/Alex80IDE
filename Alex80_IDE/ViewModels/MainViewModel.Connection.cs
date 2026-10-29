using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel
{
    private IDisposable? _connectionNoticeDismissal;

    [ObservableProperty] private bool _isConnectionNoticeVisible;
    [ObservableProperty] private bool _isConnectionInProgress;
    [ObservableProperty] private string _connectionNoticeText = string.Empty;
    [ObservableProperty] private string _connectionNoticeDetail = string.Empty;

    private void ShowConnectionNotice(string text, string detail, bool inProgress)
    {
        _connectionNoticeDismissal?.Dispose();
        _connectionNoticeDismissal = null;
        ConnectionNoticeText = text;
        ConnectionNoticeDetail = detail;
        IsConnectionInProgress = inProgress;
        IsConnectionNoticeVisible = true;
    }

    private void ShowConnectionProgress() => ShowConnectionNotice(
        "Connessione in corso…", "Attendo l'identificazione della scheda.", true);

    private void ShowConnectionEstablished()
    {
        ShowConnectionNotice($"Connessa: {CardName}", "La scheda è pronta.", false);
        _connectionNoticeDismissal = DispatcherTimer.RunOnce(HideConnectionNotice, TimeSpan.FromSeconds(2.5));
    }

    private void ShowUnidentifiedBoardNotice()
    {
        ShowConnectionNotice("La scheda non ha ancora risposto",
            "La porta è aperta. Verifica porta, baud e firmware; puoi disconnettere e riprovare.", false);
        _connectionNoticeDismissal = DispatcherTimer.RunOnce(HideConnectionNotice, TimeSpan.FromSeconds(6));
    }

    private void HideConnectionNotice()
    {
        _connectionNoticeDismissal?.Dispose();
        _connectionNoticeDismissal = null;
        IsConnectionNoticeVisible = false;
        IsConnectionInProgress = false;
    }
}
