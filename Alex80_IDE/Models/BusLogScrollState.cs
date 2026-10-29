using System;

namespace Alex80_IDE.Models;

// A browsing pause changes the current session, while only the switch updates
// the preference used at startup. Other settings saves never see the pause.
public sealed class BusLogScrollState
{
    private readonly Action<bool> savePreference;
    public bool IsEnabled { get; private set; }

    public BusLogScrollState(bool enabled, Action<bool> savePreference)
    {
        IsEnabled = enabled;
        this.savePreference = savePreference;
    }

    public bool SetManually(bool enabled)
    {
        if (IsEnabled == enabled) return false;
        IsEnabled = enabled;
        savePreference(enabled);
        return true;
    }

    public bool Suspend()
    {
        if (!IsEnabled) return false;
        IsEnabled = false;
        return true;
    }
}
