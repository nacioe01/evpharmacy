
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EVPharmacy.Data.Database;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<DosageForm> DosageForms => Set<DosageForm>();
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<MedicinePriceHistory> MedicinePriceHistories => Set<MedicinePriceHistory>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<InventoryBatch> InventoryBatches => Set<InventoryBatch>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ensures DateTime values are written as UTC and read back with Kind = Utc.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        // Keeps the existing lowercase values used by CK_users_status.
        var userStatusConverter = new ValueConverter<UserStatus, string>(
            value => value == UserStatus.Deactivated ? "deactivated" : "active",
            value => value == "deactivated" ? UserStatus.Deactivated : UserStatus.Active);

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", table =>
                table.HasCheckConstraint(
                    "CK_users_status",
                    "status IN ('active', 'deactivated')"));

            entity.HasKey(user => user.Id);

            entity.Property(user => user.Id)
                .HasColumnName("id")
                .ValueGeneratedOnAdd();

            // NOCASE: "Admin" and "admin" are treated as the same username.
            entity.Property(user => user.Username)
                .HasColumnName("username")
                .HasMaxLength(36)
                .UseCollation("NOCASE")
                .IsRequired();

            entity.HasIndex(user => user.Username)
                .IsUnique();

            entity.Property(user => user.PasswordHash)
                .HasColumnName("password_hash")
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .HasConversion(utcConverter)
                .IsRequired();

            entity.Property(user => user.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasConversion(userStatusConverter)
                .IsRequired();

            entity.Property(user => user.IsAdmin)
                .HasColumnName("is_admin")
                .HasDefaultValue(false)
                .IsRequired();
        });

        ConfigureCatalog(modelBuilder, utcConverter);
        ConfigureInventory(modelBuilder, utcConverter);
        ConfigureSales(modelBuilder, utcConverter);
    }

    private static readonly DateTime SeedDate = new(2026, 11, 4, 0, 0, 0, DateTimeKind.Utc);

    private static void ConfigureCatalog(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter)
    {
        var prescriptionConverter = new ValueConverter<PrescriptionClass, string>(
            value => value == PrescriptionClass.Controlled ? "controlled"
                   : value == PrescriptionClass.Prescription ? "rx" : "otc",
            value => value == "controlled" ? PrescriptionClass.Controlled
                   : value == "rx" ? PrescriptionClass.Prescription
                   : PrescriptionClass.OverTheCounter);

        ConfigureLookup<Category>(modelBuilder, utcConverter, "categories");
        ConfigureLookup<DosageForm>(modelBuilder, utcConverter, "dosage_forms");
        ConfigureLookup<UnitOfMeasure>(modelBuilder, utcConverter, "units_of_measure");

        modelBuilder.Entity<Category>()
            .Property(c => c.Description)
            .HasColumnName("description")
            .HasMaxLength(Category.MaxDescriptionLength);

        modelBuilder.Entity<Category>().HasData(SeedLookup<Category>(
            "Analgesics & Antipyretics", "Antibiotics", "Antihistamines & Allergy",
            "Cough & Cold", "Gastrointestinal", "Vitamins & Supplements",
            "Cardiovascular", "Diabetes", "Topical & Skin Care", "First Aid & Supplies"));

        modelBuilder.Entity<DosageForm>().HasData(SeedLookup<DosageForm>(
            "Tablet", "Capsule", "Syrup", "Suspension", "Drops", "Injection",
            "Cream", "Ointment", "Gel", "Lotion", "Solution", "Sachet",
            "Suppository", "Inhaler", "Patch"));

        modelBuilder.Entity<UnitOfMeasure>().HasData(SeedLookup<UnitOfMeasure>(
            "Piece", "Box", "Bottle", "Strip", "Sachet", "Tube", "Vial", "Pack"));

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.ToTable("medicines", table =>
            {
                table.HasCheckConstraint("CK_medicines_prices",
                    "purchase_cost_centavos >= 0 AND selling_price_centavos >= 0");
                table.HasCheckConstraint("CK_medicines_reorder", "reorder_level >= 0");
                table.HasCheckConstraint("CK_medicines_rx",
                    "prescription_class IN ('otc', 'rx', 'controlled')");
            });

            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedOnAdd();

            entity.Property(m => m.GenericName).HasColumnName("generic_name")
                .HasMaxLength(Medicine.MaxNameLength).IsRequired();
            entity.Property(m => m.BrandName).HasColumnName("brand_name")
                .HasMaxLength(Medicine.MaxNameLength);
            entity.Property(m => m.Barcode).HasColumnName("barcode")
                .HasMaxLength(Medicine.MaxBarcodeLength).UseCollation("NOCASE");
            entity.Property(m => m.CategoryId).HasColumnName("category_id");
            entity.Property(m => m.DosageFormId).HasColumnName("dosage_form_id");
            entity.Property(m => m.UnitOfMeasureId).HasColumnName("unit_of_measure_id");
            entity.Property(m => m.Strength).HasColumnName("strength")
                .HasMaxLength(Medicine.MaxStrengthLength);
            entity.Property(m => m.PurchaseCostCentavos).HasColumnName("purchase_cost_centavos");
            entity.Property(m => m.SellingPriceCentavos).HasColumnName("selling_price_centavos");
            entity.Property(m => m.ReorderLevel).HasColumnName("reorder_level").HasDefaultValue(0);
            entity.Property(m => m.PrescriptionClass).HasColumnName("prescription_class")
                .HasMaxLength(20).HasConversion(prescriptionConverter);
            entity.Property(m => m.IsVatExempt).HasColumnName("is_vat_exempt").HasDefaultValue(false);
            entity.Property(m => m.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(m => m.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);
            entity.Property(m => m.UpdatedAt).HasColumnName("updated_at").HasConversion(utcConverter);

            entity.Ignore(m => m.DisplayName);

            // NULL barcodes are allowed many times; real barcodes must be unique.
            entity.HasIndex(m => m.Barcode).IsUnique().HasFilter("barcode IS NOT NULL");
            entity.HasIndex(m => m.GenericName);
            entity.HasIndex(m => m.BrandName);
            entity.HasIndex(m => m.CategoryId);

            // Restrict: a lookup value in use can never be removed from under a medicine.
            entity.HasOne(m => m.Category).WithMany()
                .HasForeignKey(m => m.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.DosageForm).WithMany()
                .HasForeignKey(m => m.DosageFormId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(m => m.UnitOfMeasure).WithMany()
                .HasForeignKey(m => m.UnitOfMeasureId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MedicinePriceHistory>(entity =>
        {
            entity.ToTable("medicine_price_history");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(h => h.MedicineId).HasColumnName("medicine_id");
            entity.Property(h => h.OldPurchaseCostCentavos).HasColumnName("old_purchase_cost_centavos");
            entity.Property(h => h.NewPurchaseCostCentavos).HasColumnName("new_purchase_cost_centavos");
            entity.Property(h => h.OldSellingPriceCentavos).HasColumnName("old_selling_price_centavos");
            entity.Property(h => h.NewSellingPriceCentavos).HasColumnName("new_selling_price_centavos");
            entity.Property(h => h.Reason).HasColumnName("reason")
                .HasMaxLength(MedicinePriceHistory.MaxReasonLength);
            entity.Property(h => h.ChangedByUserId).HasColumnName("changed_by_user_id");
            entity.Property(h => h.ChangedAt).HasColumnName("changed_at").HasConversion(utcConverter);

            entity.HasIndex(h => new { h.MedicineId, h.ChangedAt });

            // History is never cascade-deleted with its medicine or user.
            entity.HasOne(h => h.Medicine).WithMany(m => m.PriceHistory)
                .HasForeignKey(h => h.MedicineId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(h => h.ChangedByUser).WithMany()
                .HasForeignKey(h => h.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLookup<T>(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter,
        string tableName) where T : LookupItem
    {
        modelBuilder.Entity<T>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Name).HasColumnName("name")
                .HasMaxLength(LookupItem.MaxNameLength)
                .UseCollation("NOCASE").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasConversion(utcConverter);
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasConversion(utcConverter);
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }

    private static T[] SeedLookup<T>(params string[] names) where T : LookupItem, new() =>
        names.Select((name, index) => new T
        {
            Id = index + 1,
            Name = name,
            IsActive = true,
            CreatedAt = SeedDate
        }).ToArray();
}
