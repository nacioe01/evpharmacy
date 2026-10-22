
using Microsoft.EntityFrameworkCore;
using EVPharmacy.Core.Entities;

namespace EVPharmacy.Data.Database;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

            entity.Property(user => user.Username)
                .HasColumnName("username")
                .HasMaxLength(36)
                .IsRequired();

            entity.HasIndex(user => user.Username)
                .IsUnique();

            entity.Property(user => user.Password)
                .HasColumnName("password_hash")
                .IsRequired();

            entity.Property(user => user.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(user => user.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .HasDefaultValue("active")
                .IsRequired();

            entity.Property(user => user.IsAdmin)
                .HasColumnName("is_admin")
                .HasDefaultValue(false)
                .IsRequired();
        });
    }
}
