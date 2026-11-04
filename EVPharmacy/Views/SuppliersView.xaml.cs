using EVPharmacy.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace EVPharmacy.Views;

public partial class SuppliersView : UserControl
{
    public SuppliersView()
    {
        InitializeComponent();
    }

    private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (Grid.SelectedItem is SupplierRow row &&
            DataContext is SuppliersViewModel vm &&
            e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase &&
            vm.EditCommand.CanExecute(row))
        {
            vm.EditCommand.Execute(row);
        }
    }
}
