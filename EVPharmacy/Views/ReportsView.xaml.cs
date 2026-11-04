using EVPharmacy.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace EVPharmacy.Views;

public partial class ReportsView : UserControl
{
    public ReportsView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // Reports have different columns, so the grid is rebuilt from the report each time it changes.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is INotifyPropertyChanged oldVm)
        {
            oldVm.PropertyChanged -= OnViewModelChanged;
        }

        if (e.NewValue is INotifyPropertyChanged newVm)
        {
            newVm.PropertyChanged += OnViewModelChanged;
            Rebuild();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ReportsViewModel.Table))
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        ReportGrid.Columns.Clear();
        ReportGrid.ItemsSource = null;

        if (DataContext is not ReportsViewModel { Table: { } table })
        {
            return;
        }

        var rightAligned = new Style(typeof(TextBlock));
        rightAligned.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
        rightAligned.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Stretch));

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];

            var gridColumn = new DataGridTextColumn
            {
                Header = column.Header.ToUpperInvariant(),
                Binding = new Binding($"[{i}]"),
                Width = i == 0 ? new DataGridLength(1.6, DataGridLengthUnitType.Star)
                               : new DataGridLength(1, DataGridLengthUnitType.Auto),
                MinWidth = i == 0 ? 175 : 90
            };

            if (column.IsNumber)
            {
                gridColumn.ElementStyle = rightAligned;
            }

            ReportGrid.Columns.Add(gridColumn);
        }

        ReportGrid.ItemsSource = table.Rows;
    }
}
