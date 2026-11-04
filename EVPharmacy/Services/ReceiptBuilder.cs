using EVPharmacy.Core;
using EVPharmacy.Core.Entities;
using EVPharmacy.Data.Services;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace EVPharmacy.Services;

/// <summary>
/// Builds a receipt as a FlowDocument sized for an 80 mm roll. The same builder feeds
/// the on-screen preview and the printer, so what you see is what prints.
/// </summary>
public static class ReceiptBuilder
{
    private const double PageWidthPx = 302;   // 80 mm at 96 dpi

    public static FlowDocument Build(SaleDetail sale, StoreSettings store, bool isReprint = false)
    {
        var doc = new FlowDocument
        {
            PageWidth = PageWidthPx,
            ColumnWidth = PageWidthPx,
            PagePadding = new Thickness(12, 10, 12, 14),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11.5,
            Background = Brushes.White,
            Foreground = Brushes.Black
        };

        // ----- Header -----
        doc.Blocks.Add(Center(store.StoreName.ToUpperInvariant(), bold: true, size: 14));
        AddIfAny(doc, store.Address);
        var tinLine = string.Join("   ", new[]
        {
            string.IsNullOrWhiteSpace(store.Tin) ? null : $"TIN: {store.Tin}",
            string.IsNullOrWhiteSpace(store.ContactNumber) ? null : $"Tel: {store.ContactNumber}"
        }.Where(s => s is not null));
        AddIfAny(doc, tinLine);
        AddIfAny(doc, store.PermitInfo);
        if (!sale.IsVatRegistered)
        {
            doc.Blocks.Add(Center("NON-VAT REGISTERED"));
        }

        doc.Blocks.Add(Rule());
        doc.Blocks.Add(Center("SALES RECEIPT", bold: true, size: 12.5));

        if (sale.Status == SaleStatus.Voided)
        {
            doc.Blocks.Add(Center("*** VOIDED ***", bold: true, size: 13));
            doc.Blocks.Add(Center(sale.VoidReason ?? string.Empty));
        }
        else if (isReprint)
        {
            doc.Blocks.Add(Center("*** REPRINT ***", bold: true));
        }

        doc.Blocks.Add(Rule());

        // ----- Sale info -----
        doc.Blocks.Add(Pairs(
            ("Receipt No.", sale.ReceiptNumber),
            ("Date", sale.SoldAtUtc.ToLocalTime().ToString("MMM d, yyyy  h:mm tt")),
            ("Cashier", sale.CashierName ?? "—")));
        doc.Blocks.Add(Rule());

        // ----- Items -----
        var itemsTable = NewTable();
        foreach (var line in sale.Lines)
        {
            var mark = sale.IsVatRegistered ? (line.IsVatExempt ? " E" : " V") : string.Empty;
            var rx = line.PrescriptionClass == PrescriptionClass.OverTheCounter ? string.Empty : " [Rx]";

            AddRow(itemsTable, line.MedicineName + rx, string.Empty);
            AddRow(itemsTable,
                $"  {line.Quantity:N0} x {Amount(line.UnitPriceCentavos)}{mark}",
                Amount(line.LineTotalCentavos));
        }

        doc.Blocks.Add(itemsTable);
        doc.Blocks.Add(Rule());

        // ----- Totals -----
        var totals = NewTable();
        if (sale.DiscountType != DiscountType.None)
        {
            var who = sale.DiscountType == DiscountType.Pwd ? "PWD" : "SC";
            AddRow(totals, "Subtotal", Amount(sale.SubtotalCentavos));
            if (sale.VatRemovedCentavos > 0)
            {
                AddRow(totals, "Less: VAT", "-" + Amount(sale.VatRemovedCentavos));
            }

            AddRow(totals, $"Less: {sale.DiscountBps / 100m:0.##}% {who} disc.", "-" + Amount(sale.DiscountCentavos));
        }

        AddRow(totals, "TOTAL DUE", Amount(sale.TotalCentavos), bold: true, size: 13);
        doc.Blocks.Add(totals);
        doc.Blocks.Add(Rule());

        // ----- Payment -----
        var pay = NewTable();
        switch (sale.PaymentMethod)
        {
            case PaymentMethod.Cash:
                AddRow(pay, "Cash", Amount(sale.AmountTenderedCentavos));
                AddRow(pay, "Change", Amount(sale.ChangeCentavos));
                break;
            case PaymentMethod.Card:
                AddRow(pay, "Card", Amount(sale.TotalCentavos));
                break;
            default:
                AddRow(pay, "E-wallet", Amount(sale.TotalCentavos));
                break;
        }

        if (!string.IsNullOrWhiteSpace(sale.PaymentReference))
        {
            AddRow(pay, "Ref.", sale.PaymentReference!);
        }

        doc.Blocks.Add(pay);
        doc.Blocks.Add(Rule());

        // ----- Tax breakdown -----
        var tax = NewTable();
        if (sale.IsVatRegistered)
        {
            AddRow(tax, "VATable Sales", Amount(sale.VatableSalesCentavos));
            AddRow(tax, "VAT-Exempt Sales", Amount(sale.VatExemptSalesCentavos));
            AddRow(tax, "Zero-Rated Sales", Amount(0));
            AddRow(tax, $"VAT ({sale.VatRateBps / 100m:0.##}%)", Amount(sale.VatAmountCentavos));
        }
        else
        {
            AddRow(tax, "Non-VAT Sales", Amount(sale.NonVatSalesCentavos));
        }

        doc.Blocks.Add(tax);

        if (sale.IsVatRegistered)
        {
            doc.Blocks.Add(Small("V = VATable   E = VAT-Exempt"));
        }

        // ----- Senior Citizen / PWD documentation -----
        if (sale.DiscountType != DiscountType.None)
        {
            var who = sale.DiscountType == DiscountType.Pwd ? "PWD" : "Senior Citizen";
            doc.Blocks.Add(Rule());
            doc.Blocks.Add(Pairs(
                (who, sale.CustomerName ?? string.Empty),
                ("ID No.", sale.CustomerIdNumber ?? string.Empty)));
            doc.Blocks.Add(Para("\nSignature: ______________________"));
        }

        if (sale.RxVerified)
        {
            doc.Blocks.Add(Small("Prescription checked by cashier."));
        }

        // ----- Footer -----
        if (!string.IsNullOrWhiteSpace(store.ReceiptFooter))
        {
            doc.Blocks.Add(Rule());
            doc.Blocks.Add(Center(store.ReceiptFooter!));
        }

        return doc;
    }

    // ------------------------------------------------------------------

    private static string Amount(long centavos) => Money.ToPesos(centavos).ToString("N2");

    private static void AddIfAny(FlowDocument doc, string? text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            doc.Blocks.Add(Center(text!));
        }
    }

    private static Paragraph Para(string text, bool bold = false, double? size = null)
    {
        var p = new Paragraph(new Run(text)) { Margin = new Thickness(0, 1, 0, 1) };
        if (bold) p.FontWeight = FontWeights.Bold;
        if (size is double s) p.FontSize = s;
        return p;
    }

    private static Paragraph Center(string text, bool bold = false, double? size = null)
    {
        var p = Para(text, bold, size);
        p.TextAlignment = TextAlignment.Center;
        return p;
    }

    private static Paragraph Small(string text)
    {
        var p = Para(text, size: 10);
        p.Foreground = Brushes.DimGray;
        return p;
    }

    private static Paragraph Rule() =>
        new Paragraph(new Run(new string('-', 38)))
        {
            Margin = new Thickness(0, 2, 0, 2),
            FontSize = 10,
            Foreground = Brushes.Gray
        };

    private const double ContentWidthPx = PageWidthPx - 24;   // page minus left/right padding

    private static Table NewTable(double rightWidth = 96)
    {
        var table = new Table { CellSpacing = 0, Margin = new Thickness(0) };
        table.Columns.Add(new TableColumn { Width = new GridLength(ContentWidthPx - rightWidth) });
        table.Columns.Add(new TableColumn { Width = new GridLength(rightWidth) });
        table.RowGroups.Add(new TableRowGroup());
        return table;
    }

    private static void AddRow(Table table, string left, string right, bool bold = false, double? size = null)
    {
        var row = new TableRow();

        var l = new Paragraph(new Run(left)) { Margin = new Thickness(0) };
        var r = new Paragraph(new Run(right)) { Margin = new Thickness(8, 0, 0, 0), TextAlignment = TextAlignment.Right };

        if (bold)
        {
            l.FontWeight = FontWeights.Bold;
            r.FontWeight = FontWeights.Bold;
        }

        if (size is double s)
        {
            l.FontSize = s;
            r.FontSize = s;
        }

        row.Cells.Add(new TableCell(l));
        row.Cells.Add(new TableCell(r));
        table.RowGroups[0].Rows.Add(row);
    }

    private static Table Pairs(params (string Label, string Value)[] rows)
    {
        var table = NewTable(170);
        foreach (var (label, value) in rows)
        {
            AddRow(table, label, value);
        }

        return table;
    }
}
