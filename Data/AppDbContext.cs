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
    public DbSet<ProductKit> ProductKits { get; set; }
    
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

    public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public DbSet<OrderCustomerReview> OrderCustomerReviews { get; set; }
    
    /// <summary>
    /// Gets or sets the Brands database set
    /// </summary>
    public DbSet<Brand> Brands { get; set; }
    public DbSet<ProductNameSuggestion> ProductNameSuggestions { get; set; }

    /// <summary>
    /// Gets or sets the ReserveQueue database set (очередь «беру» при уже забронированном товаре)
    /// </summary>
    public DbSet<ReserveQueue> ReserveQueue { get; set; }

    /// <summary>
    /// OTP-коды для входа по телефону
    /// </summary>
    public DbSet<PhoneLoginOtp> PhoneLoginOtps { get; set; }

    /// <summary>
    /// Аудит согласий на обработку персональных данных
    /// </summary>
    public DbSet<PersonalDataConsentLog> PersonalDataConsentLogs { get; set; }

    public DbSet<UserChild> UserChildren { get; set; }

    public DbSet<ReferralCode> ReferralCodes { get; set; }

    public DbSet<Referral> Referrals { get; set; }
    public DbSet<IncomingShipment> IncomingShipments { get; set; }
    public DbSet<IncomingShipmentExpense> IncomingShipmentExpenses { get; set; }

    public DbSet<ProductColor> ProductColors { get; set; }
    public DbSet<ProductCondition> ProductConditions { get; set; }
    public DbSet<ProductNuance> ProductNuances { get; set; }
    public DbSet<UserFavoriteProduct> UserFavoriteProducts { get; set; }

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
            entity.Property(e => e.CartAvailableAt);
            entity.Property(e => e.BoxNumber).HasMaxLength(50);
            entity.Property(e => e.Owner).HasMaxLength(50);
            entity.Property(e => e.IncomingShipmentId);
            entity.Property(e => e.Nuance).HasMaxLength(100);
            entity.Property(e => e.DiscountPercent);
            entity.HasIndex(e => e.PublishedAt);
            entity.HasIndex(e => e.IncomingShipmentId);
            entity.HasOne(e => e.IncomingShipment)
                .WithMany()
                .HasForeignKey(e => e.IncomingShipmentId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.Property(e => e.KitPartName).HasMaxLength(200);
            entity.HasIndex(e => e.KitId);
            entity.HasIndex(e => e.IsKitDisplay);
            entity.Property(e => e.IsTestProduct).HasDefaultValue(false);
            entity.HasIndex(e => e.IsTestProduct);
            entity.HasOne(e => e.Kit)
                .WithMany(k => k.Products)
                .HasForeignKey(e => e.KitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductKit>(entity =>
        {
            entity.ToTable("product_kits");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.KitPrice).HasColumnType("decimal(10,2)");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.GoogleSub).HasMaxLength(64);
            entity.Property(e => e.VkUserId);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.Property(e => e.AutoFilterByChildren).HasDefaultValue(false);
            entity.Property(e => e.DateOfBirth).HasColumnType("date");
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
            entity.HasIndex(e => e.GoogleSub).IsUnique().HasFilter("[GoogleSub] IS NOT NULL");
            entity.HasIndex(e => e.VkUserId).IsUnique().HasFilter("[VkUserId] IS NOT NULL");
        });

        modelBuilder.Entity<CartItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SessionId).IsRequired().HasMaxLength(255);
            entity.HasIndex(e => new { e.SessionId, e.ProductId });
            entity.HasIndex(e => new { e.UserId, e.ProductId });
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.Property(e => e.CartAddMode).HasMaxLength(16);
            entity.Property(e => e.KitBundleKey).HasMaxLength(36);
            entity.Property(e => e.ChargedUnitPrice).HasColumnType("decimal(10,2)");
            entity.HasIndex(e => e.KitId);
            entity.HasIndex(e => e.KitBundleKey);
            entity.HasOne(e => e.Kit)
                .WithMany()
                .HasForeignKey(e => e.KitId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OrderNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CustomerProfileLink).HasMaxLength(500);
            entity.Property(e => e.CustomerPhone).IsRequired().HasMaxLength(50);
            entity.Property(e => e.CustomerEmail).HasMaxLength(255);
            entity.Property(e => e.DeliveryMethod).HasMaxLength(50);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50).HasDefaultValue("Ожидает оплату");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.DiscountType).HasMaxLength(20).HasDefaultValue("None");
            entity.Property(e => e.FixedDiscountPercent);
            entity.Property(e => e.Condition1ItemPercent);
            entity.Property(e => e.Condition3ItemsPercent);
            entity.Property(e => e.Condition5PlusPercent);
            entity.Property(e => e.CancellationReason).HasMaxLength(500);
            entity.Property(e => e.ParentOrderId);
            entity.HasIndex(e => e.ParentOrderId);
            entity.HasOne(e => e.ParentOrder)
                .WithMany(o => o.ChildOrders)
                .HasForeignKey(e => e.ParentOrderId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.OrderNumber).IsUnique();
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => new { e.Status, e.UserId });
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ProductPrice).HasColumnType("decimal(10,2)");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.AddedToParcel).HasDefaultValue(false);
            entity.HasOne(e => e.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OrderStatusHistory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ChangedAtUtc);
            entity.HasOne(e => e.Order)
                .WithMany(o => o.StatusHistories)
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.ChangedByUser)
                .WithMany()
                .HasForeignKey(e => e.ChangedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OrderCustomerReview>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Rating);
            entity.Property(e => e.CreatedAtUtc);
            entity.Property(e => e.Comment).HasColumnType("TEXT");
            entity.Property(e => e.ReviewImagesJson).HasColumnType("TEXT");
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAtUtc);
            entity.Property(e => e.ManualCustomerName).HasMaxLength(255);
            entity.Property(e => e.ManualCustomerPhone).HasMaxLength(100);
            entity.HasOne(e => e.Order)
                .WithOne(o => o.CustomerReview)
                .HasForeignKey<OrderCustomerReview>(e => e.OrderId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReserveQueue>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ChannelId).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Username).HasMaxLength(255);
            entity.Property(e => e.FirstName).HasMaxLength(255);
            entity.Property(e => e.LastName).HasMaxLength(255);
            entity.Property(e => e.CustomerPhone).HasMaxLength(50);
            entity.HasIndex(e => e.ProductId);
            entity.HasIndex(e => new { e.ChannelId, e.PostMessageId });
            entity.HasIndex(e => new { e.ProductId, e.WebUserId }).IsUnique().HasFilter("[WebUserId] IS NOT NULL");
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.WebUser)
                .WithMany()
                .HasForeignKey(e => e.WebUserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PhoneLoginOtp>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PhoneE164).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(10);
            entity.HasIndex(e => e.PhoneE164);
        });

        modelBuilder.Entity<Brand>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<ProductNameSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(150);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<PersonalDataConsentLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ConsentKind).IsRequired().HasMaxLength(80);
            entity.Property(e => e.IpAddress).HasMaxLength(45);
            entity.Property(e => e.UserAgent).HasColumnType("TEXT");
            entity.Property(e => e.DeviceType).HasMaxLength(32);
            entity.Property(e => e.ExtraJson).HasColumnType("TEXT");
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.AcceptedAtUtc);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IncomingShipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.WeightKg).HasColumnType("decimal(10,3)");
            entity.Property(e => e.ItemCount);
            entity.Property(e => e.OrderedAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Profit).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt);
            entity.Property(e => e.UpdatedAt);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<IncomingShipmentExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.IncomingShipmentId).IsRequired(false);
            entity.Property(e => e.CreatedAt);
            entity.HasIndex(e => e.IncomingShipmentId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.IncomingShipment)
                .WithMany()
                .HasForeignKey(e => e.IncomingShipmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserChild>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DateOfBirth).HasColumnType("date");
            entity.Property(e => e.ClothingSize).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Gender).IsRequired().HasMaxLength(20);
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReferralCode>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Code).IsRequired().HasMaxLength(32);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Referral>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(30).HasDefaultValue("Pending");
            entity.Property(e => e.ReferrerRewardAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.ReferredRewardAmount).HasColumnType("decimal(10,2)");
            entity.HasIndex(e => e.ReferrerUserId);
            entity.HasIndex(e => e.ReferredUserId);
            entity.HasIndex(e => e.ReferralCodeId);
            entity.HasIndex(e => e.Status);
            entity.HasOne(e => e.ReferrerUser)
                .WithMany()
                .HasForeignKey(e => e.ReferrerUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.ReferredUser)
                .WithMany()
                .HasForeignKey(e => e.ReferredUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ReferralCode)
                .WithMany()
                .HasForeignKey(e => e.ReferralCodeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.FirstOrder)
                .WithMany()
                .HasForeignKey(e => e.FirstOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ProductColor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        modelBuilder.Entity<ProductCondition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        modelBuilder.Entity<ProductNuance>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<UserFavoriteProduct>(entity =>
        {
            entity.ToTable("user_favorite_products");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // После всех конфигураций — единый нижний регистр имён таблиц (как products, users, orders).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType == null)
            {
                var tableName = entityType.GetTableName();
                if (!string.IsNullOrEmpty(tableName))
                    entityType.SetTableName(tableName.ToLowerInvariant());
            }
        }
    }
}
