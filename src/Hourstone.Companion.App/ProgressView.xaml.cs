using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Hourstone.Companion.App;

public partial class ProgressView : UserControl
{
    public ProgressView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is ProgressViewModel previous) previous.PropertyChanged -= ModelChanged;
            if (e.NewValue is ProgressViewModel current) current.PropertyChanged += ModelChanged;
            RefreshHeaders();
        };
    }
    void ModelChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName is null) RefreshHeaders(); }
    void RefreshHeaders()
    {
        if (DataContext is not ProgressViewModel vm) return;
        var headers = new[] { vm.CharacterLabel, vm.KeystoneLabel, vm.WeeklyLabel, vm.DungeonLabel, vm.RaidLabel, vm.WorldLabel };
        for (var i = 0; i < headers.Length; i++) ProgressGrid.Columns[i].Header = headers[i];
    }
    void CloseDetails_Click(object sender, RoutedEventArgs e) { if (DataContext is ProgressViewModel vm) vm.Selected = null; }
}
