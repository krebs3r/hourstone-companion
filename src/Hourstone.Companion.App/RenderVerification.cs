using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Hourstone.Companion.App;

/// <summary>Synthetic render options; never opens a source, provider folder or browser.</summary>
public sealed record RenderProfile(string? ClientsState, bool Selected, int SelectionColumn, bool SelectionUnfocused)
{
    public static RenderProfile Parse(string[] arguments)
    {
        string? Value(string flag)
        {
            var index = Array.IndexOf(arguments, flag);
            if (index < 0) return null;
            if (index + 1 == arguments.Length || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Missing render option value: " + flag);
            return arguments[index + 1];
        }
        var state = Value("--clients-state");
        if (state is not (null or "empty" or "missing" or "outdated")) throw new ArgumentException("Unknown clients preview state.");
        var selected = arguments.Contains("--selected") || arguments.Contains("--removed");
        var columnValue = Value("--selection-column");
        var column = 0;
        if (columnValue is not null && (!int.TryParse(columnValue, NumberStyles.None, CultureInfo.InvariantCulture, out column) || column is < 0 or > 3))
            throw new ArgumentException("Render selection column must be 0 through 3.");
        var unfocused = arguments.Contains("--selection-unfocused");
        if (!selected && (columnValue is not null || unfocused)) throw new ArgumentException("Selection options require a selected character preview.");
        if (state is not null && selected) throw new ArgumentException("Client setup and character selection require separate previews.");
        return new(state, selected, column, unfocused);
    }
}

/// <summary>Assertions against the realized WPF tree before a preview is saved.</summary>
public static class RenderVerification
{
    public static void Prepare(MainWindow window, RenderProfile profile)
    {
        if (profile.ClientsState is not null) window.SetClientsPreview(profile.ClientsState);
        if (!profile.Selected) return;
        var grid = Named<DataGrid>(window, "CharacterGrid");
        Require(grid.Items.Count > 0, "Selected preview has no character.");
        grid.SelectedIndex = 0;
        grid.CurrentCell = new DataGridCellInfo(grid.Items[0], grid.Columns[profile.SelectionColumn]);
        grid.ScrollIntoView(grid.Items[0], grid.Columns[profile.SelectionColumn]);
        grid.UpdateLayout();
        if (profile.SelectionUnfocused) Named<Button>(window, "CharacterActionButton").Focus();
        else
        {
            var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
            if (row is not null) Descendants<DataGridCell>(row).FirstOrDefault(cell => cell.Column == grid.Columns[profile.SelectionColumn])?.Focus();
        }
    }

    public static IReadOnlyList<string> Verify(MainWindow window, RenderProfile profile)
    {
        var checks = new List<string>();
        var note = Named<TextBlock>(window, "FooterNote");
        var credits = Named<FrameworkElement>(window, "FooterCredits");
        var noteBounds = VisibleBounds(window, note);
        var creditBounds = VisibleBounds(window, credits);
        Require(!noteBounds.IntersectsWith(creditBounds), "Footer note overlaps the credits.");
        Require(note.TextTrimming == TextTrimming.CharacterEllipsis, "Footer note must truncate with an ellipsis in compact windows.");
        Require((VisualTreeHelper.GetParent(note) as FrameworkElement)?.ToolTip is string tooltip && tooltip == note.Text,
            "The complete footer explanation must be available as a tooltip.");
        var labels = Descendants<TextBlock>(credits).Select(label => label.Text).ToArray();
        Require(labels.Any(label => label == ((MainViewModel)window.DataContext).BuildLabel) && labels.Any(label => label.Contains("krebs3r", StringComparison.Ordinal)),
            "Footer version or author credit is absent.");
        Require(Descendants<Image>(credits).Any(image => image.IsVisible && image.Source?.ToString().EndsWith("/Heart.png", StringComparison.OrdinalIgnoreCase) == true),
            "Footer heart is absent.");
        VisibleBounds(window, Named<Button>(window, "FooterGitHubButton"));
        checks.Add("footer-visible-and-unclipped");

        if (profile.Selected)
        {
            var grid = Named<DataGrid>(window, "CharacterGrid");
            Require(grid.SelectionUnit == DataGridSelectionUnit.FullRow, "Character selection must cover a whole row.");
            var row = grid.ItemContainerGenerator.ContainerFromIndex(0) as DataGridRow;
            Require(row is not null && row.IsSelected, "The selected row was not realized.");
            var selectedRow = row!;
            var cells = Descendants<DataGridCell>(selectedRow).ToArray();
            Require(cells.Length == grid.Columns.Count && cells.All(cell => cell.IsSelected), "Selection does not cover every character column.");
            Require(cells.All(cell => cell.Background is null || cell.Background is SolidColorBrush brush && brush.Color.A == 0),
                "A cell paints over the row-wide selection background.");
            Require(cells.All(cell => cell.BorderThickness == new Thickness(0)), "Selection or focus still draws a cell-only border.");
            Require(selectedRow.Background is SolidColorBrush selected && window.FindResource("Selection") is SolidColorBrush expected && selected.Color == expected.Color,
                "Selected row does not use the theme selection color.");
            var underline = selectedRow.Template.FindName("SelectionUnderline", selectedRow) as FrameworkElement;
            Require(underline is not null && underline.IsVisible && !underline.IsHitTestVisible, "Row selection underline is absent or intercepts input.");
            var underlineBounds = underline!.TransformToAncestor(selectedRow).TransformBounds(new Rect(underline.RenderSize));
            Require(Math.Abs(underlineBounds.Left) <= 1 && Math.Abs(underlineBounds.Right - selectedRow.ActualWidth) <= 1,
                "Selection underline does not span the complete row.");
            Require(Math.Abs(underlineBounds.Bottom - selectedRow.ActualHeight) <= 1, "Selection underline is not at the row bottom.");
            checks.Add("selection-spans-every-column");
        }

        if (profile.ClientsState is not null)
        {
            var page = Named<ScrollViewer>(window, "ClientsPage");
            Require(page.IsVisible, "Client preview is on the wrong page.");
            foreach (var name in new[] { "AddonDownloadButton", "AddonGitHubButton" })
            {
                var button = Named<Button>(window, name);
                Require(button.IsEnabled && button.Focusable, "Client download links must support keyboard input.");
                VisibleBounds(window, button);
            }
            var sources = Named<StackPanel>(window, "SourcesPanel");
            var downloads = Descendants<Button>(sources).Where(button => AutomationProperties.GetAutomationId(button).StartsWith("AddonDownload-", StringComparison.Ordinal)).ToArray();
            if (profile.ClientsState == "empty") Require(downloads.Length == 0, "Empty client preview contains a source download card.");
            else
            {
                Require(downloads.Length == 1, "Missing or outdated addon preview needs one contextual download link.");
                Require(downloads[0].IsEnabled && downloads[0].Focusable, "Contextual addon download link is disabled.");
                downloads[0].BringIntoView();
                window.ChromeRoot.UpdateLayout();
                VisibleBounds(window, downloads[0]);
                var expected = profile.ClientsState == "missing" ? (window.English ? "Hourstone missing" : "Hourstone fehlt")
                    : (window.English ? "Update Hourstone" : "Hourstone aktualisieren");
                Require(Descendants<TextBlock>(sources).Any(label => label.Text == expected), "Wrong addon readiness state in preview.");
            }
            checks.Add("clients-" + profile.ClientsState + "-links-visible");
        }
        return checks;
    }

    private static T Named<T>(MainWindow window, string name) where T : FrameworkElement =>
        window.FindName(name) as T ?? throw new InvalidOperationException("Missing render verification element: " + name);

    private static Rect VisibleBounds(MainWindow window, FrameworkElement element)
    {
        Require(element.IsVisible && element.ActualWidth > 0 && element.ActualHeight > 0, "Element is not visible: " + element.Name);
        var bounds = element.TransformToAncestor(window.ChromeRoot).TransformBounds(new Rect(element.RenderSize));
        Require(bounds.Left >= -1 && bounds.Top >= -1 && bounds.Right <= window.ChromeRoot.ActualWidth + 1 && bounds.Bottom <= window.ChromeRoot.ActualHeight + 1,
            "Element is outside the preview: " + element.Name);
        for (var parent = VisualTreeHelper.GetParent(element); parent is not null && parent != window.ChromeRoot; parent = VisualTreeHelper.GetParent(parent))
            if (parent is ScrollContentPresenter viewport)
            {
                var viewBounds = viewport.TransformToAncestor(window.ChromeRoot).TransformBounds(new Rect(viewport.RenderSize));
                Require(bounds.Left >= viewBounds.Left - 1 && bounds.Top >= viewBounds.Top - 1 && bounds.Right <= viewBounds.Right + 1 && bounds.Bottom <= viewBounds.Bottom + 1,
                    "Element is clipped by its scroll viewport: " + element.Name);
            }
        return bounds;
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Render verification failed: " + message); }
}
