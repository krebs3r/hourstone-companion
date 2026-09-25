using System;
using System.Windows;
using System.Windows.Controls;
namespace Hourstone.Companion.App;
public partial class StoreSetupView : UserControl
{
    public event EventHandler? PrimaryRequested;
    public event EventHandler? SecondaryRequested;
    public event EventHandler? StartupSettingsRequested;
    public StoreSetupView() => InitializeComponent();
    void Primary_Click(object sender, RoutedEventArgs e) => PrimaryRequested?.Invoke(this, EventArgs.Empty);
    void StartupSettings_Click(object sender, RoutedEventArgs e) => StartupSettingsRequested?.Invoke(this, EventArgs.Empty);
    void Secondary_Click(object sender, RoutedEventArgs e) => SecondaryRequested?.Invoke(this, EventArgs.Empty);
}
