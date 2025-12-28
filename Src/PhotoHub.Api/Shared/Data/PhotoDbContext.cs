using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Data;

public class PhotoDbContext : DbContext
{
    public PhotoDbContext(DbContextOptions<PhotoDbContext> options) : base(options)
    {
    }

    public DbSet<PhotoEntity> Photos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PhotoEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FullPath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Extension).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.FullPath).IsUnique();
            entity.HasIndex(e => e.FileName);
            
            // Configurar fechas como timestamp sin timezone
            // Convertir UTC a Unspecified al guardar, y viceversa al leer
            entity.Property(e => e.CreatedDate)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            
            entity.Property(e => e.ModifiedDate)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
            
            entity.Property(e => e.ScannedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}

