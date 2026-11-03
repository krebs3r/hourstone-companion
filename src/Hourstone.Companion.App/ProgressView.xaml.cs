using System;
using System.Collections.Specialized;
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
        ProgressScroller.SizeChanged += (_, _) => FitGrid();
        ProgressStack.SizeChanged += (_, _) => FitGrid();
        // The height is explicit, so a filtered row count no longer resizes the stack by itself.
        ((INotifyCollectionChanged)ProgressGrid.Items).CollectionChanged += (_, _) => FitGrid();
    }
    // Like the playtime grid, the table fills the window. It never drops below its rows (up to the former 420 DIP),
    // so open details and small windows scroll the page instead of shrinking the table.
    // ActualHeight, not ViewportHeight: the viewport is only updated after SizeChanged, and there is no horizontal scroll bar.
    // The stack is top-aligned, so its height is its content and changes whenever details open or close.
    void FitGrid()
    {
        if (ProgressScroller.ActualHeight <= 0 || ProgressGrid.ActualHeight <= 0) return;
        var header = double.IsNaN(ProgressGrid.ColumnHeaderHeight) ? 40 : ProgressGrid.ColumnHeaderHeight;
        var rows = Math.Min(420, header + ProgressGrid.Items.Count * ProgressGrid.RowHeight + SystemParameters.HorizontalScrollBarHeight);
        var fill = ProgressScroller.ActualHeight - (ProgressStack.ActualHeight - ProgressGrid.ActualHeight);
        var height = Math.Max(Math.Max(ProgressGrid.MinHeight, rows), fill);
        // Layout rounding snaps to device pixels, which are smaller than 1 DIP above 100% scaling.
        if (double.IsNaN(ProgressGrid.Height) || Math.Abs(ProgressGrid.Height - height) > 0.01) ProgressGrid.Height = height;
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
