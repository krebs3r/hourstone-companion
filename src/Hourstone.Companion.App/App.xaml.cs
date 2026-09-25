using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Velopack;
namespace Hourstone.Companion.App;

public partial class App : Application { }
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        bool render = args.Contains("--render"), demo = render || args.Contains("--demo");
        bool renderFailed = false;
        try
        {
            AppRuntime.Initialize(args);
            AppDistribution.Current.InitializeDirectDistribution();
            if (!AppRuntime.OwnsInstance && !AppDistribution.Current.IsStore)
            { if (!AppRuntime.StartInBackground) AppRuntime.SignalExistingWindow(); return 0; }
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow(demo, render); app.MainWindow = window;
            if (AppRuntime.IsSmokeTest)
            {
                window.Loaded += async (_, _) =>
                {
                    var initialized = await window.RunStartupSmokeAsync();
                    var database = Path.Combine(UserSettings.DataDirectory, "companion.sqlite");
                    var report = Path.Combine(UserSettings.DataDirectory, "smoke-result.json");
                    var passed = initialized && window.IsVisible && File.Exists(database);
                    File.WriteAllText(report, JsonSerializer.Serialize(new { passed, distribution = AppDistribution.Current.Kind.ToString(), dataDirectory = UserSettings.DataDirectory, databaseCreated = File.Exists(database), windowVisible = window.IsVisible, dispatcherRunning = true }));
                    renderFailed = !passed; window.Quit();
                };
            }
            if (render)
            {
                string Option(string flag, string fallback) { var index = Array.IndexOf(args, flag); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
                var profile = RenderProfile.Parse(args);
                var path = Path.GetFullPath(Option("--render", "preview.png"));
                var scale = double.Parse(Option("--scale", "1"), CultureInfo.InvariantCulture);
                var width = double.Parse(Option("--width", "1536"), CultureInfo.InvariantCulture);
                var height = double.Parse(Option("--height", "992"), CultureInfo.InvariantCulture);
                if (scale < .5 || scale > 3 || width < 960 || width > 3000 || height < 600 || height > 2000) throw new ArgumentException("Invalid render size.");
                window.Width = width; window.Height = height; window.ShowInTaskbar = false; window.ShowActivated = false; window.Left = -20000; window.Top = -20000;
                window.SetRenderTheme(Option("--theme", "dark"));
                if (args.Contains("--english")) window.SetRenderLanguage(true);
                if (args.Contains("--all-classes")) ((MainViewModel)window.DataContext).SetObservations(MainViewModel.AllClassDemoData());
                if (args.Contains("--settings-draft")) window.SetSettingsDraftPreview();
                if (args.Contains("--long-names")) window.SetLongNamePreview();
                if (args.Contains("--removed")) window.SetRemovedPreview();
                window.SetRenderPage(profile.ClientsState is null ? Option("--page", "overview") : "clients");
                window.Loaded += (_, _) => window.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        window.ChromeRoot.Measure(new Size(width, height)); window.ChromeRoot.Arrange(new Rect(0, 0, width, height)); window.ChromeRoot.UpdateLayout();
                        RenderVerification.Prepare(window, profile);
                        window.ChromeRoot.UpdateLayout();
                        var checks = args.Contains("--verify-render") ? RenderVerification.Verify(window, profile) : Array.Empty<string>();
                        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(window.ChromeRoot);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!); using (var stream = File.Create(path)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); png.Save(stream); }
                        if (args.Contains("--verify-render")) File.WriteAllText(Path.ChangeExtension(path, ".checks.json"), JsonSerializer.Serialize(new { passed = true, checks }));
                    }
                    catch (Exception ex) { renderFailed = true; Console.Error.WriteLine(ex); }
                    finally { window.Quit(); }

                }, DispatcherPriority.ApplicationIdle);
            }
            if (!AppRuntime.StartInBackground || demo || !AppRuntime.OwnsInstance || (AppDistribution.Current.IsStore && StoreImportService.RequiresSetup)) window.Show();
            var exitCode = app.Run();
            return renderFailed ? 1 : exitCode;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); if (!demo) MessageBox.Show("Hourstone Companion konnte nicht starten. Prüfe die lokalen App-Daten und Dateiberechtigungen. / Could not start. Check local app data and file permissions.", "Hourstone Companion", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
        finally { AppRuntime.Release(); }
    }
}
