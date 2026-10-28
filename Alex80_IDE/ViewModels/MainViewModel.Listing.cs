using Alex80_IDE.Models;
using CommunityToolkit.Mvvm.ComponentModel;

public partial class MainViewModel
{
    private DocumentViewModel? _executionListing;
    private uint? _latestExecutionAddress;
    private int _listingLogStartIndex;

    [ObservableProperty]
    private bool _followListingExecution = true;

    [ObservableProperty]
    private LogElement? _selectedLog;

    partial void OnFollowListingExecutionChanged(bool value)
    {
        if (!value) return;
        SelectedLog = null;
        _executionListing?.HighlightExecutionAddress(_latestExecutionAddress);
    }

    partial void OnSelectedLogChanged(LogElement? value)
    {
        if (value is null) return;
        FollowListingExecution = false;
        uint? address = null;
        var index = LogTable.IndexOf(value);
        // I log precedenti all'assemblata attiva non appartengono a questo listing.
        if (!value.IsInjected && !value.IsReset)
        {
            for (var i = index; i >= _listingLogStartIndex; i--)
            {
                var log = LogTable[i];
                if (log.IsReset) break;
                if (!log.IsInstructionFetch) continue;
                address = log.Address;
                break;
            }
        }
        _executionListing?.HighlightExecutionAddress(address);
        if (_executionListing is { ExecutionLine: > 0 }) SelectedDocument = _executionListing;
    }

    private void TrackListingExecution(LogElement log)
    {
        if (log.IsReset) _latestExecutionAddress = null;
        else if (log.IsInstructionFetch) _latestExecutionAddress = log.Address;
        else return;

        if (FollowListingExecution) _executionListing?.HighlightExecutionAddress(_latestExecutionAddress);
    }

    private void ResetListingExecution(bool discardListing = false)
    {
        _executionListing?.HighlightExecutionAddress(null);
        if (discardListing) _executionListing = null;
        _latestExecutionAddress = null;
        _listingLogStartIndex = LogTable?.Count ?? 0;
        SelectedLog = null;
    }
}
