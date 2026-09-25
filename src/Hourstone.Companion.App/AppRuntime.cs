using System;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Security.Principal;
using System.Threading;

namespace Hourstone.Companion.App;

/// <summary>One writer across distribution channels. The legacy session mutex remains compatible with older releases.</summary>
public static class AppRuntime
{
    static Mutex? userGate, legacyGate;
    public static bool OwnsInstance { get; private set; }
    public static bool OtherInstanceRunning => !OwnsInstance;
    public static bool StartInBackground { get; private set; }
    public static bool IsPreview { get; private set; }
    public static bool IsSmokeTest { get; private set; }
    public static string? SmokeDataDirectory { get; private set; }
    public static void Initialize(string[] args)
    {
        IsPreview = args.Contains("--demo") || args.Contains("--render");
        IsSmokeTest = args.Contains("--smoke-test");
        if (IsSmokeTest)
        {
            var index = Array.IndexOf(args, "--data-directory");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("--smoke-test requires an isolated --data-directory.");
            SmokeDataDirectory = Path.GetFullPath(args[index + 1]);
            var legacy = Path.GetFullPath(UserSettings.LegacyDataDirectory);
            if (SmokeDataDirectory.StartsWith(legacy, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Smoke tests cannot use the existing Hourstone data directory.");
        }
        StartInBackground = args.Contains("--background");
        if (AppDistribution.Current.IsStore)
        {
            var activation = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
            StartInBackground |= activation?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
        }
        if (IsPreview) { OwnsInstance = true; return; }
        TryAcquireInstance();
    }
    public static bool TryAcquireInstance()
    {
        if (OwnsInstance) return true;
        var sid = WindowsIdentity.GetCurrent().User?.Value ?? throw new InvalidOperationException("Windows user identity is unavailable.");
        var suffix = IsSmokeTest ? ".Smoke." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SmokeDataDirectory!)))[..16] : "";
        userGate = new(false, "Global\\HourstoneCompanion." + sid + suffix);
        if (!Acquire(userGate)) { userGate.Dispose(); userGate = null; return false; }
        legacyGate = new(false, "Local\\HourstoneCompanion" + suffix);
        if (!Acquire(legacyGate))
        {
            userGate.ReleaseMutex(); userGate.Dispose(); userGate = null;
            legacyGate.Dispose(); legacyGate = null; return false;
        }
        return OwnsInstance = true;
    }
    static bool Acquire(Mutex mutex)
    {
        try { return mutex.WaitOne(0); }
        catch (AbandonedMutexException) { return true; }
    }
    public static void SignalExistingWindow()
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            if (EventWaitHandle.TryOpenExisting("Local\\HourstoneCompanion.Show", out var signal))
            { using (signal) signal.Set(); return; }
            Thread.Sleep(100);
        }
    }
    public static void Release()
    {
        if (legacyGate is not null) { legacyGate.ReleaseMutex(); legacyGate.Dispose(); legacyGate = null; }
        if (userGate is not null) { userGate.ReleaseMutex(); userGate.Dispose(); userGate = null; }
        OwnsInstance = false;
    }
}
