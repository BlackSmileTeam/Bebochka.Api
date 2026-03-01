using Microsoft.EntityFrameworkCore;
using Bebochka.Api.Models;

namespace Bebochka.Api.Data;

/// <summary>
/// Database context for the Bebochka application
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the AppDbContext class
    /// </summary>
    /// <param name="options">Database context options</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Products database set
    /// </summary>
    public DbSet<Product> Products { get; set; }
    
    /// <summary>
    /// Gets or sets the Users database set
    /// </summary>
    public DbSet<User> Users { get; set; }
    
    /// <summary>
    /// Gets or sets the CartItems database set
    /// </summary>
    public DbSet<CartItem> CartItems { get; set; }
    
    /// <summary>
    /// Gets or sets the Orders database set
    /// </summary>
    public DbSet<Order> Orders { get; set; }
    
    /// <summary>
    /// Gets or sets the OrderItems database set
    /// </summary>
    public DbSet<OrderItem> OrderItems { get; set; }
    
    /// <summary>
    /// Gets or sets the Announcements database set
    /// </summary>
    public DbSet<Announcement> Announcements { get; set; }
    
    /// <summary>
    /// Gets or sets the Brands database set
    /// </summary>
    public DbSet<Brand> Brands { get; set; }

    /// <summary>
    /// Gets or sets the TelegramErrors database set
    /// </summary>
    public DbSet<TelegramError> TelegramErrors { get; set; }

    /// <summary>
    /// Configures the entity models and their relationships
    /// </summary>
    /// <param name="modelBuilder">Model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Color).HasMaxLength(50);
            entity.Property(e => e.Size).HasMaxLength(20);
            entity.Property(e => e.Price).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ImagesJson).HasColumnName("Images");
            entity.Ignore(e => e.Images);
            entity.Property(e => e.PublishedAt);
            entity.HasIndex(e => e.PublishedAt);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.TelegramUserId);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.TelegramUserId).IsUnique().HasFilter("[TelegramUserId] IS NOT NULL");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => new { e.SessionId, e.ProductId });
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CustomerPhone).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerEmail).HasMaxLength(255);
            entity.Property(e => e.DeliveryMethod).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Ожидает оплату");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.UserId });
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProductPrice).HasColumnType("decimal(10,2)");
            entity.HasOne(e => e.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.ProductIdsJson).HasColumnName("ProductIds");
            entity.Ignore(e => e.ProductIds);
            entity.Property(e => e.CollageImagesJson).HasColumnName("CollageImages");
            entity.Ignore(e => e.CollageImages);
            entity.HasIndex(e => e.ScheduledAt);
            entity.HasIndex(e => e.IsSent);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<TelegramError>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            entity.Property(e => e.Details).HasMaxLength(5000);
            entity.Property(e => e.ErrorType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProductInfo).HasMaxLength(500);
            entity.Property(e => e.ChannelId).HasMaxLength(100);
            entity.Property(e => e.ErrorDate).IsRequired();
            entity.HasIndex(e => e.ErrorDate);
            entity.HasIndex(e => e.ErrorType);
        });
    }
}
