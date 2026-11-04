
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EVPharmacy.Data.Database;

// Sales and sale items.
public partial class AppDbContext
{
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();

    private static void ConfigureSales(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter)
    {
        ConfigureSettings(modelBuilder, utcConverter);
        ConfigureAudit(modelBuilder, utcConverter);

        modelBuilder.Entity<Sale>(entity =>
        {
            entity.ToTable("sales", table =>
            {
                table.HasCheckConstraint("CK_sales_amounts",
                    "subtotal_centavos >= 0 AND total_centavos >= 0 AND amount_tendered_centavos >= total_centavos AND change_centavos >= 0");
                table.HasCheckConstraint("CK_sales_status", "status IN ('Completed', 'Voided')");
                table.HasCheckConstraint("CK_sales_discount_type", "discount_type IN ('None', 'SeniorCitizen', 'Pwd')");
                table.HasCheckConstraint("CK_sales_payment", "payment_method IN ('Cash', 'Card', 'EWallet')");
            });
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(s => s.ReceiptNumber).HasColumnName("receipt_number").HasMaxLength(30).IsRequired();
            entity.Property(s => s.SoldAt).HasColumnName("sold_at").HasConversion(utcConverter);
            entity.Property(s => s.CashierUserId).HasColumnName("cashier_user_id");
            entity.Property(s => s.SubtotalCentavos).HasColumnName("subtotal_centavos");
            entity.Property(s => s.TotalCentavos).HasColumnName("total_centavos");
            entity.Property(s => s.PaymentMethod).HasColumnName("payment_method").HasMaxLength(20).HasConversion<string>();
            entity.Property(s => s.AmountTenderedCentavos).HasColumnName("amount_tendered_centavos");
            entity.Property(s => s.ChangeCentavos).HasColumnName("change_centavos");
            entity.Property(s => s.PaymentReference).HasColumnName("payment_reference").HasMaxLength(Sale.MaxReferenceLength);
            entity.Property(s => s.RxVerified).HasColumnName("rx_verified").HasDefaultValue(false);
            entity.Property(s => s.IsVatRegistered).HasColumnName("is_vat_registered").HasDefaultValue(false);
            entity.Property(s => s.VatRateBps).HasColumnName("vat_rate_bps").HasDefaultValue(0);
            entity.Property(s => s.DiscountType).HasColumnName("discount_type").HasMaxLength(20).HasConversion<string>().HasDefaultValue(DiscountType.None);
            entity.Property(s => s.DiscountBps).HasColumnName("discount_bps").HasDefaultValue(0);
            entity.Property(s => s.DiscountCentavos).HasColumnName("discount_centavos").HasDefaultValue(0L);
            entity.Property(s => s.VatRemovedCentavos).HasColumnName("vat_removed_centavos").HasDefaultValue(0L);
            entity.Property(s => s.VatableSalesCentavos).HasColumnName("vatable_sales_centavos").HasDefaultValue(0L);
            entity.Property(s => s.VatExemptSalesCentavos).HasColumnName("vat_exempt_sales_centavos").HasDefaultValue(0L);
            entity.Property(s => s.NonVatSalesCentavos).HasColumnName("non_vat_sales_centavos").HasDefaultValue(0L);
            entity.Property(s => s.VatAmountCentavos).HasColumnName("vat_amount_centavos").HasDefaultValue(0L);
            entity.Property(s => s.CustomerName).HasColumnName("customer_name").HasMaxLength(Sale.MaxCustomerNameLength);
            entity.Property(s => s.CustomerIdNumber).HasColumnName("customer_id_number").HasMaxLength(Sale.MaxCustomerIdLength);
            entity.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).HasConversion<string>();
            entity.Property(s => s.VoidReason).HasColumnName("void_reason").HasMaxLength(Sale.MaxVoidReasonLength);
            entity.Property(s => s.VoidedAt).HasColumnName("voided_at").HasConversion(utcConverter);
            entity.Property(s => s.VoidedByUserId).HasColumnName("voided_by_user_id");

            entity.HasIndex(s => s.ReceiptNumber).IsUnique();
            entity.HasIndex(s => s.SoldAt);

            // History is never cascade-deleted.
            entity.HasOne(s => s.CashierUser).WithMany()
                .HasForeignKey(s => s.CashierUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.VoidedByUser).WithMany()
                .HasForeignKey(s => s.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SaleItem>(entity =>
        {
            entity.ToTable("sale_items", table =>
            {
                table.HasCheckConstraint("CK_sale_items_qty", "quantity > 0");
                table.HasCheckConstraint("CK_sale_items_money",
                    "unit_price_centavos >= 0 AND unit_cost_centavos >= 0 AND line_total_centavos >= 0");
            });
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(i => i.SaleId).HasColumnName("sale_id");
            entity.Property(i => i.MedicineId).HasColumnName("medicine_id");
            entity.Property(i => i.BatchId).HasColumnName("batch_id");
            entity.Property(i => i.MedicineName).HasColumnName("medicine_name").HasMaxLength(300).IsRequired();
            entity.Property(i => i.Quantity).HasColumnName("quantity");
            entity.Property(i => i.UnitPriceCentavos).HasColumnName("unit_price_centavos");
            entity.Property(i => i.UnitCostCentavos).HasColumnName("unit_cost_centavos");
            entity.Property(i => i.LineTotalCentavos).HasColumnName("line_total_centavos");
            entity.Property(i => i.IsVatExempt).HasColumnName("is_vat_exempt").HasDefaultValue(false);
            entity.Property(i => i.VatAmountCentavos).HasColumnName("vat_amount_centavos").HasDefaultValue(0L);
            entity.Property(i => i.VatRemovedCentavos).HasColumnName("vat_removed_centavos").HasDefaultValue(0L);
            entity.Property(i => i.DiscountCentavos).HasColumnName("discount_centavos").HasDefaultValue(0L);
            entity.Property(i => i.NetCentavos).HasColumnName("net_centavos").HasDefaultValue(0L);
            entity.Property(i => i.PrescriptionClass).HasColumnName("prescription_class")
                .HasMaxLength(20).HasConversion<string>();

            entity.HasIndex(i => i.SaleId);
            entity.HasIndex(i => i.BatchId);
            entity.HasIndex(i => i.MedicineId);

            entity.HasOne(i => i.Sale).WithMany(s => s.Items)
                .HasForeignKey(i => i.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Medicine).WithMany()
                .HasForeignKey(i => i.MedicineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(i => i.Batch).WithMany()
                .HasForeignKey(i => i.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
