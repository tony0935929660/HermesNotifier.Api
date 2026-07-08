using HermesNotifier.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace HermesNotifier.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }

    public DbSet<Product> Products { get; set; }

    public DbSet<ProductLog> ProductLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.LineId)
                .IsRequired()
                .HasMaxLength(100);

            entity.HasIndex(e => e.LineId)
                .IsUnique();

            entity.Property(e => e.Name)
                .HasMaxLength(100);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

            entity.Property(e => e.SubscribedUntil)
                .HasDefaultValueSql("DATEADD(YEAR, 1, DATEADD(HOUR, 8, GETUTCDATE()))");
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => e.ProductId)
                .IsUnique();

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Price)
                .HasPrecision(18, 2);

            entity.Property(e => e.ImageUrl)
                .HasMaxLength(500);

            entity.Property(e => e.ProductUrl)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Color)
                .HasMaxLength(50);

            entity.Property(e => e.IsAvailable)
                .IsRequired()
                .HasDefaultValue(true);

            entity.Property(e => e.AvailabilityStatus)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("InStock");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

            // 建立索引以加速查詢上架中的商品
            entity.HasIndex(e => e.IsAvailable);

            // 建立索引以加速三態查詢（InStock/OutOfStock/NotFound）
            entity.HasIndex(e => e.AvailabilityStatus);

            // 建立索引以加速「只查快取已過期、需重新抓取」的商品
            entity.HasIndex(e => e.CacheExpiresAt);
        });

        modelBuilder.Entity<ProductLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId)
                .IsRequired();

            entity.Property(e => e.Action)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(e => e.LoggedAt)
                .HasDefaultValueSql("DATEADD(HOUR, 8, GETUTCDATE())");

            // 建立外鍵關聯
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            // 建立索引
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => e.LoggedAt);
            entity.HasIndex(e => e.Action);
        });
    }
}
