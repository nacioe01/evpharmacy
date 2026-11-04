using EVPharmacy.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace EVPharmacy.Views;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
    }

    // Double-clicking a medicine adds it to the cart (same as the Add button).
    private void OnProductDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProductGrid.SelectedItem is PosProductRow row &&
            row.CanAdd &&
            DataContext is PosViewModel vm &&
            e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            vm.AddToCartCommand.Execute(row);
        }
    }
}
