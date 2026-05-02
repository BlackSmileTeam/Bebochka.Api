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

    public DbSet<OrderStatusHistory> OrderStatusHistories { get; set; }

    public DbSet<OrderCustomerReview> OrderCustomerReviews { get; set; }
    
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
    public DbSet<IncomingShipment> IncomingShipments { get; set; }
    public DbSet<IncomingShipmentExpense> IncomingShipmentExpenses { get; set; }

    /// <summary>
    /// Configures the entity models and their relationships
    /// </summary>
    /// <param name="modelBuilder">Model builder instance</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // MySQL на Linux с lower_case_table_names=1 хранит имена таблиц в нижнем регистре (Users -> users).
        // Без этого Pomelo обращается к `Users`, а физическая таблица — `users` → "Table doesn't exist".
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (entityType.BaseType == null && !string.IsNullOrEmpty(entityType.GetTableName()))
                entityType.SetTableName(entityType.GetTableName()!.ToLowerInvariant());
        }

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
            entity.Property(e => e.TelegramFileIdsJson).HasColumnName("TelegramFileIds");
            entity.Ignore(e => e.TelegramFileIds);
            entity.Property(e => e.PublishedAt);
            entity.Property(e => e.CartAvailableAt);
            entity.Property(e => e.TelegramMessageId);
            entity.Property(e => e.TelegramChatId).HasMaxLength(50);
            entity.Property(e => e.BoxNumber).HasMaxLength(50);
            entity.Property(e => e.IncomingShipmentId);
            entity.HasIndex(e => e.PublishedAt);
            entity.HasIndex(e => new { e.TelegramChatId, e.TelegramMessageId });
            entity.HasIndex(e => e.IncomingShipmentId);
            entity.HasOne(e => e.IncomingShipment)
                .WithMany()
                .HasForeignKey(e => e.IncomingShipmentId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Username).IsRequired().HasMaxLength(50);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.TelegramUserId);
            entity.Property(e => e.Phone).HasMaxLength(20);
            entity.Property(e => e.GoogleSub).HasMaxLength(64);
            entity.Property(e => e.IsAdmin).HasDefaultValue(false);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.TelegramUserId).IsUnique().HasFilter("[TelegramUserId] IS NOT NULL");
            entity.HasIndex(e => e.Phone).IsUnique().HasFilter("[Phone] IS NOT NULL");
            entity.HasIndex(e => e.GoogleSub).IsUnique().HasFilter("[GoogleSub] IS NOT NULL");
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
            entity.Property(e => e.TelegramCommentChatId);
            entity.Property(e => e.TelegramCommentMessageId);
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
            entity.Property(e => e.Comment).HasColumnType("TEXT");
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasOne(e => e.Order)
                .WithOne(o => o.CustomerReview)
                .HasForeignKey<OrderCustomerReview>(e => e.OrderId)
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
            entity.Property(e => e.Message).IsRequired().HasMaxLength(2000);
            entity.Property(e => e.Details).HasColumnType("TEXT"); // Use TEXT for larger error details
            entity.Property(e => e.ErrorType).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ProductInfo).HasMaxLength(1000);
            entity.Property(e => e.ChannelId).HasMaxLength(100);
            entity.Property(e => e.ErrorDate).IsRequired();
            entity.HasIndex(e => e.ErrorDate);
            entity.HasIndex(e => e.ErrorType);
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
            entity.Property(e => e.OrderedAmount).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Profit).HasColumnType("decimal(10,2)");
            entity.Property(e => e.Notes).HasMaxLength(1000);
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<IncomingShipmentExpense>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(120);
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)");
            entity.HasIndex(e => e.IncomingShipmentId);
            entity.HasOne(e => e.IncomingShipment)
                .WithMany()
                .HasForeignKey(e => e.IncomingShipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
