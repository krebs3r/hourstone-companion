using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using Velopack;

namespace Hourstone.Companion.App;

public enum DistributionKind { Development, VelopackInstalled, VelopackPortable, Store }

/// <summary>Package identity is checked before touching the direct-distribution updater.</summary>
public sealed class AppDistribution
{
    public static AppDistribution Current { get; } = new();
    public bool IsStore { get; }
    public bool IsTestPackage { get; }
    public DistributionKind Kind => IsStore ? DistributionKind.Store : Detect(false, directInstalled, directPortable);
    bool directInstalled, directPortable;
    public string? InstallDirectory { get; private set; }
    public string? StoreProductId { get; }
    public string DataDirectory => AppRuntime.SmokeDataDirectory ?? (IsStore
        ? Windows.Storage.ApplicationData.Current.LocalFolder.Path : UserSettings.LegacyDataDirectory);
    AppDistribution()
    {
        uint size = 0;
        IsStore = GetCurrentPackageFullName(ref size, IntPtr.Zero) == 122;
        if (!IsStore) return;
        IsTestPackage = Windows.ApplicationModel.Package.Current.Id.Name.EndsWith(".LocalTest", StringComparison.Ordinal);
        var file = Path.Combine(AppContext.BaseDirectory, "distribution.json");
        if (File.Exists(file))
        {
            using var json = JsonDocument.Parse(File.ReadAllText(file));
            if (json.RootElement.TryGetProperty("storeProductId", out var id) && id.ValueKind == JsonValueKind.String)
            {
                var value = id.GetString();
                if (value is { Length: >= 8 and <= 32 } && System.Linq.Enumerable.All(value, char.IsAsciiLetterOrDigit)) StoreProductId = value;
            }
        }
    }
    public static DistributionKind Detect(bool packaged, bool installed, bool portable) => packaged ? DistributionKind.Store
        : !installed ? DistributionKind.Development : portable ? DistributionKind.VelopackPortable : DistributionKind.VelopackInstalled;
    public void InitializeDirectDistribution()
    {
        if (IsStore) return;
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        var manager = new UpdateManager(new Velopack.Sources.GithubSource("https://github.com/krebs3r/hourstone-companion", null, false));
        directInstalled = manager.IsInstalled; directPortable = manager.IsPortable;
        InstallDirectory = Velopack.Locators.VelopackLocator.Current.RootAppDir;
    }
    public string UpdatesDescription(bool english) => IsStore
        ? english ? "New versions are available through Microsoft Store. You can also check for updates there." : "Neue Versionen erhältst du über den Microsoft Store. Dort kannst du auch nach Updates suchen."
        : english ? "This copy receives updates from GitHub." : "Diese Ausgabe erhält Updates über GitHub.";
    public BrowserLaunchResult OpenStore() => ProductLinks.OpenAddress(!IsTestPackage && StoreProductId is not null
        ? "ms-windows-store://pdp/?ProductId=" + StoreProductId : "ms-windows-store://downloadsandupdates");
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
