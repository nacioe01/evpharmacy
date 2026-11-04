
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EVPharmacy.Data.Database;

// Store settings (single row).
public partial class AppDbContext
{
    public DbSet<StoreSettings> StoreSettingsRows => Set<StoreSettings>();

    private static void ConfigureSettings(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter)
    {
        modelBuilder.Entity<StoreSettings>(entity =>
        {
            entity.ToTable("store_settings", table =>
            {
                table.HasCheckConstraint("CK_store_settings_singleton", "id = 1");
                table.HasCheckConstraint("CK_store_settings_rates",
                    "vat_rate_bps BETWEEN 0 AND 10000 AND senior_discount_bps BETWEEN 0 AND 10000 AND pwd_discount_bps BETWEEN 0 AND 10000");
            });
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(s => s.StoreName).HasColumnName("store_name").HasMaxLength(StoreSettings.MaxNameLength).IsRequired();
            entity.Property(s => s.Address).HasColumnName("address").HasMaxLength(StoreSettings.MaxAddressLength);
            entity.Property(s => s.Tin).HasColumnName("tin").HasMaxLength(StoreSettings.MaxTinLength);
            entity.Property(s => s.PermitInfo).HasColumnName("permit_info").HasMaxLength(StoreSettings.MaxPermitLength);
            entity.Property(s => s.ContactNumber).HasColumnName("contact_number").HasMaxLength(StoreSettings.MaxContactLength);
            entity.Property(s => s.ReceiptFooter).HasColumnName("receipt_footer").HasMaxLength(StoreSettings.MaxFooterLength);
            entity.Property(s => s.IsVatRegistered).HasColumnName("is_vat_registered");
            entity.Property(s => s.VatRateBps).HasColumnName("vat_rate_bps");
            entity.Property(s => s.SeniorDiscountBps).HasColumnName("senior_discount_bps");
            entity.Property(s => s.PwdDiscountBps).HasColumnName("pwd_discount_bps");
            entity.Property(s => s.IsConfirmed).HasColumnName("is_confirmed");
            entity.Property(s => s.UpdatedAt).HasColumnName("updated_at").HasConversion(utcConverter);

            // Defaults: 12% VAT, 20% Senior Citizen and PWD discounts. An administrator
            // must review and save them once (IsConfirmed) before they are treated as confirmed.
            entity.HasData(new StoreSettings
            {
                Id = StoreSettings.SingletonId,
                StoreName = "EV Pharmacy",
                IsVatRegistered = true,
                VatRateBps = 1200,
                SeniorDiscountBps = 2000,
                PwdDiscountBps = 2000,
                IsConfirmed = false
            });
        });
    }
}
