using System.IO;
using Hourstone.Companion.App;
using Xunit;

namespace Hourstone.Companion.App.Tests;

public sealed class SettingsDraftTests
{
    static SettingsValues Initial => new("Gaming PC", "dark", "de", true);

    [Fact]
    public void LoadedSettingsDoNotEnableSaving()
    {
        var draft = new SettingsDraft(Initial);
        Assert.True(draft.IsValid); Assert.False(draft.IsDirty); Assert.False(draft.CanSave(false));
    }

    [Theory]
    [InlineData("Laptop", "dark", "de", true)]
    [InlineData("Gaming PC", "light", "de", true)]
    [InlineData("Gaming PC", "system", "de", true)]
    [InlineData("Gaming PC", "dark", "en", true)]
    [InlineData("Gaming PC", "dark", "de", false)]
    public void EveryUserPreferenceRequiresAnExplicitSave(string name, string theme, string language, bool autostart)
    {
        var draft = new SettingsDraft(Initial); draft.Update(new(name, theme, language, autostart));
        Assert.True(draft.CanSave(false)); Assert.Equal(Initial, draft.Saved);
        draft.Update(Initial); Assert.False(draft.IsDirty); Assert.False(draft.CanSave(false));
    }

    [Fact]
    public void WhitespaceNormalizationDoesNotInventAChange()
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { DeviceName = " Gaming PC  " });
        Assert.False(draft.IsDirty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("PC\tName")]
    [InlineData("PC\u007fName")]
    public void InvalidNamesCannotBeSaved(string name)
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { DeviceName = name });
        Assert.False(draft.IsValid); Assert.False(draft.Save(_ => throw new Exception("Must not persist")));
    }

    [Fact]
    public void DeviceNameLimitCountsUtf8Bytes()
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { DeviceName = new string('ä', 64) });
        Assert.True(draft.IsValid);
        draft.Update(draft.Current with { DeviceName = new string('ä', 65) }); Assert.False(draft.IsValid);
    }

    [Theory]
    [InlineData("invalid", "de")]
    [InlineData("dark", "invalid")]
    public void UnknownPreferenceValuesCannotBeSaved(string theme, string language)
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { Theme = theme, Language = language });
        Assert.False(draft.IsValid); Assert.False(draft.CanSave(false));
    }

    [Fact]
    public void BusySyncKeepsDraftAndSkipsPersistence()
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { Language = "en" });
        Assert.False(draft.Save(_ => throw new Exception("Must not persist"), busy: true));
        Assert.True(draft.IsDirty); Assert.True(draft.CanSave(false)); Assert.Equal(Initial, draft.Saved);
    }

    [Fact]
    public void FailedSavePreservesBaselineAndDraftForRetry()
    {
        var draft = new SettingsDraft(Initial); var expected = Initial with { DeviceName = "Laptop", Theme = "system" };
        draft.Update(expected);
        Assert.Throws<IOException>(() => draft.Save(_ => throw new IOException("Read-only folder")));
        Assert.Equal(Initial, draft.Saved); Assert.Equal(expected, draft.Current);
        Assert.Equal(SettingsFeedbackState.Failed, draft.Feedback); Assert.True(draft.CanSave(false));
        SettingsValues? persisted = null;
        Assert.True(draft.Save(value => persisted = value)); Assert.Equal(expected, persisted);
        Assert.Equal(expected, draft.Saved); Assert.False(draft.IsDirty);
        Assert.Equal(SettingsFeedbackState.Saved, draft.Feedback); Assert.Null(draft.FailureDetail);
    }

    [Fact]
    public void RepeatedInitializationEventsKeepSavedFeedbackAndDoNotWriteAgain()
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { Theme = "system" });
        int writes = 0; Assert.True(draft.Save(_ => writes++));
        draft.Update(draft.Current);
        Assert.False(draft.Save(_ => writes++)); Assert.Equal(1, writes);
        Assert.Equal(SettingsFeedbackState.Saved, draft.Feedback); Assert.False(draft.IsDirty);
    }

    [Fact]
    public void ANewEditClearsOldFeedbackButRetainsTheSavedValues()
    {
        var draft = new SettingsDraft(Initial); draft.Update(Initial with { Theme = "light" }); draft.Save(_ => { });
        draft.Update(draft.Current with { DeviceName = "Laptop" });
        Assert.Equal("Gaming PC", draft.Saved.DeviceName); Assert.Equal(SettingsFeedbackState.None, draft.Feedback);
        Assert.True(draft.IsDirty);
    }
}
