using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace SharpSelecta.App.Views;

// Excel's double-click on a column border: the width that fits the column's widest text, among all rows
// (not only the ones on screen), and its header.
public static class DataGridColumnFitter
{
    public static double? FitWidth(DataGrid grid, DataGridColumn column)
    {
        if (column is not DataGridTextColumn { Binding: { } binding } || grid.ItemsSource is not { } items ||
            Realized<DataGridColumnHeader>(grid, column) is not { } header)
            return null;

        header.Measure(Size.Infinity);
        var width = header.DesiredSize.Width;
        header.InvalidateMeasure();

        // A cell on screen shows the font and the room around the text (margins, grid line) to add.
        if (Realized<DataGridCell>(grid, column) is not { Content: TextBlock { Bounds.Width: > 0 } sample } cell)
            return Math.Ceiling(width);

        var chrome = cell.Bounds.Width - sample.Bounds.Width;
        var reader = new TextBlock();
        reader.Bind(TextBlock.TextProperty, binding);
        var measurer = new TextBlock
        {
            FontFamily = sample.FontFamily, FontSize = sample.FontSize, FontWeight = sample.FontWeight,
            FontStyle = sample.FontStyle, FontStretch = sample.FontStretch,
        };

        var texts = new HashSet<string>();
        foreach (var item in items)
        {
            reader.DataContext = item;
            if (!string.IsNullOrEmpty(reader.Text))
                texts.Add(reader.Text);
        }

        foreach (var text in texts)
        {
            measurer.Text = text;
            measurer.Measure(Size.Infinity);
            width = Math.Max(width, measurer.DesiredSize.Width + chrome);
        }

        return Math.Ceiling(width);
    }

    private static T? Realized<T>(DataGrid grid, DataGridColumn column) where T : Control =>
        grid.GetVisualDescendants().OfType<T>()
            .FirstOrDefault(control => control.IsVisible && DataGridColumn.GetColumnContainingElement(control) == column);
}
