using Alex80_IDE.Models;
using CommunityToolkit.Mvvm.Input;

public partial class MainViewModel
{
    private readonly BusLogScrollState _busLogScroll;

    public bool IsAutoScrollEnabled
    {
        get => _busLogScroll.IsEnabled;
        set
        {
            if (_busLogScroll.SetManually(value)) OnPropertyChanged();
        }
    }

    [RelayCommand]
    private void SuspendBusLogAutoScroll()
    {
        if (_busLogScroll.Suspend()) OnPropertyChanged(nameof(IsAutoScrollEnabled));
    }
}
