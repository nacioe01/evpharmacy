
using EVPharmacy.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EVPharmacy.Data.Database;

// Audit trail.
public partial class AppDbContext
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    private static void ConfigureAudit(
        ModelBuilder modelBuilder,
        ValueConverter<DateTime, DateTime> utcConverter)
    {
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("audit_logs");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(a => a.OccurredAt).HasColumnName("occurred_at").HasConversion(utcConverter);
            entity.Property(a => a.UserId).HasColumnName("user_id");
            entity.Property(a => a.Username).HasColumnName("username").HasMaxLength(60);
            entity.Property(a => a.Action).HasColumnName("action").HasMaxLength(AuditLog.MaxActionLength).IsRequired();
            entity.Property(a => a.EntityType).HasColumnName("entity_type").HasMaxLength(AuditLog.MaxEntityTypeLength);
            entity.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(AuditLog.MaxEntityIdLength);
            entity.Property(a => a.Summary).HasColumnName("summary").HasMaxLength(AuditLog.MaxSummaryLength).IsRequired();
            entity.Property(a => a.Details).HasColumnName("details").HasMaxLength(AuditLog.MaxDetailsLength);

            entity.HasIndex(a => a.OccurredAt);
            entity.HasIndex(a => a.UserId);

            entity.HasOne(a => a.User).WithMany()
                .HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
