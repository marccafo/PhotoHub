using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Data;

public class PhotoDbContext : DbContext
{
    public PhotoDbContext(DbContextOptions<PhotoDbContext> options) : base(options)
    {
    }

    public DbSet<PhotoEntity> Photos { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<FolderPermission> FolderPermissions { get; set; }

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

        // Configure User entity
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        // Configure Folder entity
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(500);
            entity.HasIndex(e => e.Path).IsUnique();
            entity.HasIndex(e => e.ParentFolderId);
            
            // Self-referencing relationship for folder hierarchy
            entity.HasOne(e => e.ParentFolder)
                .WithMany(e => e.SubFolders)
                .HasForeignKey(e => e.ParentFolderId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });

        // Configure FolderPermission entity
        modelBuilder.Entity<FolderPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Foreign keys
            entity.HasOne(e => e.User)
                .WithMany(e => e.FolderPermissions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Folder)
                .WithMany(e => e.Permissions)
                .HasForeignKey(e => e.FolderId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.GrantedByUser)
                .WithMany()
                .HasForeignKey(e => e.GrantedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Unique constraint: one permission record per user-folder combination
            entity.HasIndex(e => new { e.UserId, e.FolderId }).IsUnique();
            
            entity.Property(e => e.GrantedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}

