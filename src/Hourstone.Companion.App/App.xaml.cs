using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
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
        VelopackApp.Build().SetAutoApplyOnStartup(false).Run();
        bool render = args.Contains("--render"), demo = render || args.Contains("--demo");
        using var mutex = new Mutex(true, "Local\\HourstoneCompanion" + (demo ? ".Preview" : ""), out bool first);
        if (!first && !render) { if (demo || args.Contains("--background")) return 0; for (int attempt = 0; attempt < 50; attempt++) { if (EventWaitHandle.TryOpenExisting("Local\\HourstoneCompanion.Show", out var signal)) { using (signal) signal.Set(); return 0; } Thread.Sleep(100); } return 0; }
        try
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow(demo, render); app.MainWindow = window;
            if (render)
            {
                string Option(string flag, string fallback) { var index = Array.IndexOf(args, flag); return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback; }
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
                window.SetRenderPage(Option("--page", "overview"));
                window.Loaded += (_, _) => window.Dispatcher.InvokeAsync(() =>
                {
                    window.ChromeRoot.Measure(new Size(width, height)); window.ChromeRoot.Arrange(new Rect(0, 0, width, height)); window.ChromeRoot.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width * scale), (int)Math.Ceiling(height * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32); bitmap.Render(window.ChromeRoot);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!); using (var stream = File.Create(path)) { var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap)); png.Save(stream); }
                    window.Quit(); app.Shutdown();
                }, DispatcherPriority.ApplicationIdle);
            }
            if (!args.Contains("--background") || demo) window.Show();
            return app.Run();
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); if (!demo) MessageBox.Show("Hourstone Companion konnte nicht starten. Prüfe die lokalen App-Daten und Dateiberechtigungen. / Could not start. Check local app data and file permissions.", "Hourstone Companion", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
    }
}
