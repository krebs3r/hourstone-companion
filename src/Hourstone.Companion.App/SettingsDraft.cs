using System;
using Hourstone.Companion.Core;

namespace Hourstone.Companion.App;

public sealed record SettingsValues(string DeviceName, string Theme, string Language, bool Autostart)
{
    public SettingsValues Normalize() => this with { DeviceName = DeviceName.Trim() };
    public UserSettings ToPreferences() => new() { Theme = Theme, Language = Language, Autostart = Autostart };
}

public enum SettingsFeedbackState { None, Saved, Failed }

/// <summary>Keeps edits separate from persisted settings until every save operation succeeds.</summary>
public sealed class SettingsDraft
{
    public SettingsValues Saved { get; private set; }
    public SettingsValues Current { get; private set; }
    public SettingsFeedbackState Feedback { get; private set; }
    public string? FailureDetail { get; private set; }
    public bool IsDirty => Current != Saved;
    public bool IsValid => ObservationRules.ValidText(Current.DeviceName, 128)
        && Current.Theme is "dark" or "light" or "system" && Current.Language is "de" or "en";

    public SettingsDraft(SettingsValues saved) { Saved = Current = saved.Normalize(); }
    public bool CanSave(bool busy) => IsDirty && IsValid && !busy;

    public void Update(SettingsValues value)
    {
        var next = value.Normalize();
        if (next == Current) return;
        Current = next; Feedback = SettingsFeedbackState.None; FailureDetail = null;
    }

    public bool Save(Action<SettingsValues> persist, bool busy = false)
    {
        if (!CanSave(busy)) return false;
        var candidate = Current;
        try
        {
            persist(candidate);
            Saved = candidate; Feedback = SettingsFeedbackState.Saved; FailureDetail = null;
            return true;
        }
        catch (Exception ex)
        {
            Feedback = SettingsFeedbackState.Failed; FailureDetail = ex.Message;
            throw;
        }
    }
}
