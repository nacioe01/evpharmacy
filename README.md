# EV Pharmacy

A desktop Point-of-Sale and inventory system for a single-computer pharmacy in the Philippines. It handles medicine records, batch and expiry tracking, selling, VAT and Senior Citizen / PWD discounts, receipts, user accounts, audit logging, backups and reports.

Built with C#, WPF (MVVM) and SQLite. Data is stored locally; no internet connection or server is needed.

## Features

| Area | What it does |
|---|---|
| **Dashboard** | Today's sales, medicine count, low-stock and expiry counts, and a list of items needing attention. |
| **Medicines** | Generic and brand name, barcode, category, dosage form, strength, unit, cost, price, reorder level, prescription class and a VAT-exempt flag. Price changes are logged. |
| **Catalog lists** | Manage categories, dosage forms and units of measure. |
| **Suppliers** | Supplier directory linked to stock receiving. |
| **Inventory** | Stock levels, batches with expiry dates, and a permanent movement history. |
| **Stock receiving** | Multi-item deliveries and opening balances. Expired batches are rejected. |
| **Batch control** | Hold, recall or release a batch. Held, recalled and expired stock can never be sold. |
| **Adjustments** | Damage, write-off, loss, supplier return and count corrections, each with a reason. |
| **Point of Sale** | Product search, cart, quantity limits, Cash / Card / E-wallet, change calculation, and a prescription check for Rx items. Stock is taken first-expiry-first-out (FEFO). |
| **Tax and discounts** | Configurable VAT, Senior Citizen and PWD rules. Customer name and ID are required for discounted sales. |
| **Receipts** | 80 mm receipt preview and printing, with VAT breakdown and VOIDED / REPRINT banners. |
| **Sales history** | Search past sales, view and reprint receipts. Administrators can void a sale, which returns its stock. |
| **Reports** | Daily sales, payment methods, top sellers and margin, Senior/PWD sales, voided sales, inventory valuation, expiry and low stock. Exports to CSV for Excel. |
| **Users** | Staff and administrator accounts, password reset, deactivation. |
| **Audit log** | Who did what and when: data changes, voids, logins and failed logins, backups. |
| **Backup and restore** | Manual backup, an automatic daily backup (newest 14 kept), validated restore with a safety copy first. |

Reports, Users, Audit log, Backup and Settings are visible to administrators only.

## Requirements

- Windows 10 or later
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (to build)
- Visual Studio 2026 (recommended) or the `dotnet` CLI

## Getting started

```powershell
git clone <repository-url>
cd EVPharmacy
dotnet build
dotnet run --project EVPharmacy
```

Or open `EVPharmacy.slnx` in Visual Studio and press F5.

On first launch you will be asked to create the administrator account. Then:

1. Sign in and open **Settings**. Check the VAT and discount percentages, enter your store details and TIN, and save.
2. Add **Suppliers**, then **Medicines**. Tick **VAT-exempt** only on medicines that genuinely qualify.
3. Enter existing stock with **Inventory → Opening balance**, or record deliveries with **Receive stock**.
4. You are ready to sell from **Point of Sale**.

## Where your data lives

| What | Location |
|---|---|
| Database | `%LOCALAPPDATA%\EVPharmacy\pharmacy.db` |
| Backups | `%LOCALAPPDATA%\EVPharmacy\Backups\` |
| Error logs | `%LOCALAPPDATA%\EVPharmacy\Logs\` |

Copy backups to a USB drive or cloud folder as well. A backup on the same computer does not protect against a failed disk.

To use a separate database for development or testing, set the `EVPHARMACY_DATA_DIR` environment variable to another folder before starting the app. The real data is then untouched.

## Project structure

```
EVPharmacy.slnx
├── EVPharmacy.Core     Entities, TaxCalculator, FEFO allocator, password hashing. No dependencies on UI or database.
├── EVPharmacy.Data     EF Core + SQLite: DbContext, migrations, services (sales, inventory, reports, audit, backup).
└── EVPharmacy          WPF application: views, view models (MVVM), themes, receipt builder, dialogs.
```

Key design choices:

- **Money is stored as integer centavos**, never floating point.
- **Product master data is separate from stock.** Stock is tracked per batch.
- **History is never deleted.** Sales, stock movements and price changes are append-only; mistakes are voided or deactivated, not removed.
- **Sales and stock changes are transactional.** A sale, its stock deduction and its movements save together or not at all.
- **Prices and tax rules are snapshotted** on every sale, so changing settings never alters past sales.
- **Foreign keys are enforced** and all queries are parameterised.
- **Database migrations** run automatically at startup, with a backup taken first.

## Tax and discount rules

All figures are VAT-inclusive prices in centavos, rounded half away from zero per line.

- **Regular sale, VAT-registered store:** VAT = price × rate ÷ (1 + rate). A VAT-exempt medicine has no VAT.
- **Senior Citizen / PWD purchase of medicines:** VAT is removed first, then the discount percentage is taken on the VAT-exclusive price. An item that is already VAT-exempt only gets the discount.
- **Store not VAT-registered:** no VAT is computed; the discount applies to the price.

The percentages and VAT registration come from **Settings**, and a medicine is only treated as VAT-exempt when its own flag says so. Nothing is assumed.

## Limitations

- Receipts are **not BIR-accredited invoices**: there are no authority-to-print or serial-number ranges. Confirm your invoicing obligations with your accountant.
- Tax rates and rules can change. Review them with your accountant or the BIR.
- Not yet supported: split payments, cash-drawer shifts, customer and doctor records for prescription sales, per-item discount exclusion, barcode-scanner-specific workflow, several barcodes per medicine, cloud backup.

## Contributing

See `CLAUDE.md` for the project rules (architecture, database and UI conventions). New schema changes go through an EF Core migration:

```powershell
dotnet ef migrations add <Name> --project EVPharmacy.Data --startup-project EVPharmacy.Data
```

Set `EVPHARMACY_DATA_DIR` to a scratch folder first so the real database is not touched.
