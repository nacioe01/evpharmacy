using EVPharmacy.Data.Services;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace EVPharmacy.Services;

/// <summary>Loads a sale and sends its receipt to a printer chosen in the standard Windows print dialog.</summary>
public class ReceiptPrinter
{
    private readonly SalesService _sales;
    private readonly SettingsService _settings;

    public ReceiptPrinter(SalesService sales, SettingsService settings)
    {
        _sales = sales;
        _settings = settings;
    }

    /// <summary>Returns false when the sale is missing or the user cancelled the print dialog.</summary>
    public async Task<bool> PrintAsync(int saleId, bool isReprint)
    {
        var sale = await _sales.GetSaleAsync(saleId);
        if (sale is null)
        {
            return false;
        }

        var store = await _settings.GetAsync();

        // A document can only belong to one viewer, so the printer gets its own copy.
        var document = ReceiptBuilder.Build(sale, store, isReprint);

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        document.PageHeight = double.NaN;
        document.PageWidth = dialog.PrintableAreaWidth > 0
            ? Math.Min(dialog.PrintableAreaWidth, 302)
            : 302;
        document.ColumnWidth = document.PageWidth;

        var paginator = ((IDocumentPaginatorSource)document).DocumentPaginator;
        dialog.PrintDocument(paginator, $"Receipt {sale.ReceiptNumber}");
        return true;
    }
}
