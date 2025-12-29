using Microsoft.EntityFrameworkCore;
using PhotoHub.API.Shared.Models;

namespace PhotoHub.API.Shared.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    // PhotoEntity removed - use Asset instead
    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetExif> AssetExifs { get; set; }
    public DbSet<AssetThumbnail> AssetThumbnails { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Folder> Folders { get; set; }
    public DbSet<FolderPermission> FolderPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // PhotoEntity configuration removed - use Asset instead

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
        
        // Configure Asset entity
        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FullPath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Checksum).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Extension).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.FullPath).IsUnique();
            entity.HasIndex(e => e.Checksum);
            entity.HasIndex(e => e.FileName);
            entity.HasIndex(e => e.FolderId);
            entity.HasIndex(e => e.OwnerId);
            
            entity.HasOne(e => e.Owner)
                .WithMany(u => u.Assets)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Folder)
                .WithMany(f => f.Assets)
                .HasForeignKey(e => e.FolderId)
                .OnDelete(DeleteBehavior.SetNull);
            
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
        
        // Configure AssetExif entity
        modelBuilder.Entity<AssetExif>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CameraMake).HasMaxLength(200);
            entity.Property(e => e.CameraModel).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Keywords).HasMaxLength(1000);
            
            entity.HasOne(e => e.Asset)
                .WithOne(a => a.Exif)
                .HasForeignKey<AssetExif>(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.AssetId).IsUnique();
            
            entity.Property(e => e.DateTimeOriginal)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.HasValue && v.Value.Kind == DateTimeKind.Utc 
                        ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) 
                        : v,
                    v => v.HasValue 
                        ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) 
                        : null);
            
            entity.Property(e => e.ExtractedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
        
        // Configure AssetThumbnail entity
        modelBuilder.Entity<AssetThumbnail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.FilePath).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Format).IsRequired().HasMaxLength(20);
            
            entity.HasOne(e => e.Asset)
                .WithMany(a => a.Thumbnails)
                .HasForeignKey(e => e.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(e => e.AssetId);
            entity.HasIndex(e => new { e.AssetId, e.Size }).IsUnique(); // One thumbnail per size per asset
            
            entity.Property(e => e.CreatedAt)
                .HasColumnType("timestamp without time zone")
                .HasConversion(
                    v => v.Kind == DateTimeKind.Utc ? DateTime.SpecifyKind(v, DateTimeKind.Unspecified) : v,
                    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
        });
    }
}

