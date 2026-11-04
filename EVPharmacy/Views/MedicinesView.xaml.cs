using EVPharmacy.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace EVPharmacy.Views;

public partial class MedicinesView : UserControl
{
    public MedicinesView()
    {
        InitializeComponent();
    }

    // Double-clicking a row opens it for editing (same as the Edit button).
    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is MedicineRow row &&
            DataContext is MedicinesViewModel vm &&
            e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase &&
            vm.EditCommand.CanExecute(row))
        {
            vm.EditCommand.Execute(row);
        }
    }
}
