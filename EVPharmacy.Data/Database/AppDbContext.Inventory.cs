
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EVPharmacy.Data.Database;

// Suppliers, stock receiving, batches and stock movements.
public partial class AppDbContext
{
    private static void ConfigureInventory(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.ToTable("suppliers");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(s => s.Name).HasColumnName("name")
                .HasMaxLength(Supplier.MaxNameLength).UseCollation("NOCASE").IsRequired();
            entity.Property(s => s.ContactPerson).HasColumnName("contact_person")
                .HasMaxLength(Supplier.MaxContactLength);
            entity.Property(s => s.Phone).HasColumnName("phone").HasMaxLength(Supplier.MaxPhoneLength);
            entity.Property(s => s.Email).HasColumnName("email").HasMaxLength(Supplier.MaxEmailLength);
            entity.Property(s => s.Address).HasColumnName("address").HasMaxLength(Supplier.MaxAddressLength);
            entity.Property(s => s.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(s => s.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasConversion(utcConverter);
            entity.HasIndex(s => s.Name).IsUnique();
        });

        modelBuilder.Entity<StockReceipt>(entity =>
        {
            entity.ToTable("stock_receipts", table =>
                table.HasCheckConstraint("CK_stock_receipts_type",
                    "receipt_type IN ('Delivery', 'OpeningBalance')"));
            entity.HasKey(r => r.Id);
            entity.Property(r => r.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(r => r.ReceiptType).HasColumnName("receipt_type")
                .HasMaxLength(20).HasConversion<string>();
            entity.Property(r => r.SupplierId).HasColumnName("supplier_id");
            entity.Property(r => r.InvoiceNumber).HasColumnName("invoice_number")
                .HasMaxLength(StockReceipt.MaxInvoiceLength);
            entity.Property(r => r.ReceivedDate).HasColumnName("received_date");
            entity.Property(r => r.Notes).HasColumnName("notes").HasMaxLength(StockReceipt.MaxNotesLength);
            entity.Property(r => r.ReceivedByUserId).HasColumnName("received_by_user_id");
            entity.Property(r => r.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);

            entity.HasIndex(r => r.ReceivedDate);
            entity.HasIndex(r => r.SupplierId);

            entity.HasOne(r => r.Supplier).WithMany()
                .HasForeignKey(r => r.SupplierId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(r => r.ReceivedByUser).WithMany()
                .HasForeignKey(r => r.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InventoryBatch>(entity =>
        {
            entity.ToTable("inventory_batches", table =>
            {
                table.HasCheckConstraint("CK_inventory_batches_qty",
                    "quantity_on_hand >= 0 AND quantity_received >= 0");
                table.HasCheckConstraint("CK_inventory_batches_cost", "unit_cost_centavos >= 0");
                table.HasCheckConstraint("CK_inventory_batches_status",
                    "status IN ('Available', 'Quarantined', 'Recalled')");
            });
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(b => b.MedicineId).HasColumnName("medicine_id");
            entity.Property(b => b.StockReceiptId).HasColumnName("stock_receipt_id");
            entity.Property(b => b.BatchNumber).HasColumnName("batch_number")
                .HasMaxLength(InventoryBatch.MaxBatchNumberLength).UseCollation("NOCASE").IsRequired();
            entity.Property(b => b.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(b => b.QuantityReceived).HasColumnName("quantity_received");
            entity.Property(b => b.QuantityOnHand).HasColumnName("quantity_on_hand");
            entity.Property(b => b.UnitCostCentavos).HasColumnName("unit_cost_centavos");
            entity.Property(b => b.Status).HasColumnName("status")
                .HasMaxLength(20).HasConversion<string>();
            entity.Property(b => b.StatusNote).HasColumnName("status_note")
                .HasMaxLength(InventoryBatch.MaxStatusNoteLength);
            entity.Property(b => b.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);
            entity.Property(b => b.UpdatedAt).HasColumnName("updated_at").HasConversion(utcConverter);

            // The same batch number of a medicine is one batch.
            entity.HasIndex(b => new { b.MedicineId, b.BatchNumber }).IsUnique();
            // FEFO lookups: earliest expiry first.
            entity.HasIndex(b => new { b.MedicineId, b.ExpiryDate });

            entity.HasOne(b => b.Medicine).WithMany()
                .HasForeignKey(b => b.MedicineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(b => b.StockReceipt).WithMany(r => r.Batches)
                .HasForeignKey(b => b.StockReceiptId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StockMovement>(entity =>
        {
            entity.ToTable("stock_movements", table =>
                table.HasCheckConstraint("CK_stock_movements_type",
                    "movement_type IN ('OpeningBalance', 'Receipt', 'Sale', 'Adjustment', 'Return')"));
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(m => m.MedicineId).HasColumnName("medicine_id");
            entity.Property(m => m.BatchId).HasColumnName("batch_id");
            entity.Property(m => m.MovementType).HasColumnName("movement_type")
                .HasMaxLength(20).HasConversion<string>();
            entity.Property(m => m.QuantityChange).HasColumnName("quantity_change");
            entity.Property(m => m.BalanceAfter).HasColumnName("balance_after");
            entity.Property(m => m.AdjustmentReason).HasColumnName("adjustment_reason")
                .HasMaxLength(30).HasConversion<string>();
            entity.Property(m => m.StockReceiptId).HasColumnName("stock_receipt_id");
            entity.Property(m => m.SaleId).HasColumnName("sale_id");
            entity.Property(m => m.Notes).HasColumnName("notes").HasMaxLength(StockMovement.MaxNotesLength);
            entity.Property(m => m.UserId).HasColumnName("user_id");
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);

            entity.HasIndex(m => new { m.MedicineId, m.CreatedAt });
            entity.HasIndex(m => m.BatchId);

            entity.HasOne(m => m.Medicine).WithMany()
                .HasForeignKey(m => m.MedicineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Batch).WithMany()
                .HasForeignKey(m => m.BatchId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.Sale).WithMany()
                .HasForeignKey(m => m.SaleId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.StockReceipt).WithMany()
                .HasForeignKey(m => m.StockReceiptId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.User).WithMany()
                .HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
