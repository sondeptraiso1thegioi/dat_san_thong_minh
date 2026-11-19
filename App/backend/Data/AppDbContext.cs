using DatSan.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DatSan.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Venue> Venues => Set<Venue>();
    public DbSet<Court> Courts => Set<Court>();
    public DbSet<PriceRule> PriceRules => Set<PriceRule>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<VipSubscription> VipSubscriptions => Set<VipSubscription>();
    public DbSet<AiSetting> AiSettings => Set<AiSetting>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var user = modelBuilder.Entity<User>();
        user.ToTable("users", "dbo");
        user.HasKey(x => x.Id);
        user.Property(x => x.Id).HasColumnName("id");
        user.Property(x => x.Name).HasColumnName("name").HasMaxLength(100);
        user.Property(x => x.Email).HasColumnName("email").HasMaxLength(150);
        user.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(20).IsUnicode(false);
        user.Property(x => x.PasswordHash).HasColumnName("password_hash").HasMaxLength(100).IsUnicode(false);
        user.Property(x => x.Role).HasColumnName("role").HasMaxLength(10).IsUnicode(false);
        user.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
        user.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
        user.Property(x => x.Lat).HasColumnName("lat");
        user.Property(x => x.Lng).HasColumnName("lng");
        user.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3)
            .HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')"); // Matches df_users_created.

        // Các bảng dưới đây đã có sẵn trong SQL Server; chỉ ánh xạ, không tạo/sửa bảng.
        var venue = modelBuilder.Entity<Venue>();
        venue.ToTable("venues", "dbo");
        venue.HasKey(x => x.Id);
        venue.Property(x => x.Id).HasColumnName("id");
        venue.Property(x => x.OwnerId).HasColumnName("owner_id");
        venue.Property(x => x.Name).HasColumnName("name").HasMaxLength(150);
        venue.Property(x => x.Address).HasColumnName("address").HasMaxLength(255);
        venue.Property(x => x.Lat).HasColumnName("lat");
        venue.Property(x => x.Lng).HasColumnName("lng");
        venue.Property(x => x.OpenTime).HasColumnName("open_time").HasColumnType("time(0)");
        venue.Property(x => x.CloseTime).HasColumnName("close_time").HasColumnType("time(0)");
        venue.Property(x => x.Amenities).HasColumnName("amenities");
        venue.Property(x => x.Photos).HasColumnName("photos");
        venue.Property(x => x.Description).HasColumnName("description");
        venue.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
        venue.Property(x => x.RejectReason).HasColumnName("reject_reason");
        venue.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3)
            .HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        venue.HasMany(x => x.Courts).WithOne(x => x.Venue).HasForeignKey(x => x.VenueId);
        venue.HasMany(x => x.PriceRules).WithOne().HasForeignKey(x => x.VenueId);
        venue.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId);

        var court = modelBuilder.Entity<Court>();
        court.ToTable("courts", "dbo");
        court.HasKey(x => x.Id);
        court.Property(x => x.Id).HasColumnName("id");
        court.Property(x => x.VenueId).HasColumnName("venue_id");
        court.Property(x => x.Sport).HasColumnName("sport").HasMaxLength(15).IsUnicode(false);
        court.Property(x => x.Name).HasColumnName("name").HasMaxLength(50);
        court.Property(x => x.Environment).HasColumnName("environment").HasMaxLength(10).IsUnicode(false);
        court.Property(x => x.IsActive).HasColumnName("is_active");

        var price = modelBuilder.Entity<PriceRule>();
        // The table has an overlap trigger; EF must not use OUTPUT without INTO when writing it.
        price.ToTable("price_rules", "dbo", table => table.HasTrigger("trg_price_rules_no_overlap"));
        price.HasKey(x => x.Id);
        price.Property(x => x.Id).HasColumnName("id");
        price.Property(x => x.VenueId).HasColumnName("venue_id");
        price.Property(x => x.Sport).HasColumnName("sport").HasMaxLength(15).IsUnicode(false);
        price.Property(x => x.Weekday).HasColumnName("weekday");
        price.Property(x => x.StartHour).HasColumnName("start_hour");
        price.Property(x => x.EndHour).HasColumnName("end_hour");
        price.Property(x => x.PricePerHour).HasColumnName("price_per_hour");

        var booking = modelBuilder.Entity<Booking>();
        booking.ToTable("bookings", "dbo", table => table.HasTrigger("trg_bookings_no_overlap"));
        booking.HasKey(x => x.Id);
        booking.Property(x => x.Id).HasColumnName("id");
        booking.Property(x => x.UserId).HasColumnName("user_id");
        booking.Property(x => x.CourtId).HasColumnName("court_id");
        booking.Property(x => x.StartAt).HasColumnName("start_at").HasPrecision(3);
        booking.Property(x => x.EndAt).HasColumnName("end_at").HasPrecision(3);
        booking.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
        booking.Property(x => x.Price).HasColumnName("price");
        booking.Property(x => x.Source).HasColumnName("source").HasMaxLength(10).IsUnicode(false);
        booking.Property(x => x.HoldExpiresAt).HasColumnName("hold_expires_at").HasPrecision(3);
        booking.Property(x => x.CancelReason).HasColumnName("cancel_reason");
        booking.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3)
            .HasDefaultValueSql("TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00')");
        booking.HasOne(x => x.Court).WithMany().HasForeignKey(x => x.CourtId);

        var vip = modelBuilder.Entity<VipSubscription>();
        vip.ToTable("vip_subscriptions", "dbo");
        vip.HasKey(x => x.Id);
        vip.Property(x => x.Id).HasColumnName("id");
        vip.Property(x => x.VenueId).HasColumnName("venue_id");
        vip.Property(x => x.PlanId).HasColumnName("plan_id");
        vip.Property(x => x.StartAt).HasColumnName("start_at").HasPrecision(3);
        vip.Property(x => x.EndAt).HasColumnName("end_at").HasPrecision(3);
        vip.Property(x => x.Status).HasColumnName("status").HasMaxLength(10).IsUnicode(false);
        vip.Property(x => x.CreatedAt).HasColumnName("created_at").HasPrecision(3);

        // Chỉ ánh xạ các cột cần cho việc đánh dấu hoàn tiền; các cột khác giữ nguyên trong DB.
        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("payments", "dbo");
        payment.HasKey(x => x.Id);
        payment.Property(x => x.Id).HasColumnName("id");
        payment.Property(x => x.Type).HasColumnName("type").HasMaxLength(10).IsUnicode(false);
        payment.Property(x => x.BookingId).HasColumnName("booking_id");
        payment.Property(x => x.Amount).HasColumnName("amount");
        payment.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsUnicode(false);
        payment.Property(x => x.PayoutStatus).HasColumnName("payout_status").HasMaxLength(10).IsUnicode(false);

        var setting = modelBuilder.Entity<AiSetting>();
        setting.ToTable("ai_settings", "dbo");
        setting.HasKey(x => x.Key);
        setting.Property(x => x.Key).HasColumnName("key").HasMaxLength(50).IsUnicode(false);
        setting.Property(x => x.Value).HasColumnName("value");
        setting.Property(x => x.Description).HasColumnName("description");
        setting.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasPrecision(3);
    }
}
