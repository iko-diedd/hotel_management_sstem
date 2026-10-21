using HousekeepingApi.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace HousekeepingApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<HousekeepingNotice> HousekeepingNotices => Set<HousekeepingNotice>();
    public DbSet<RoomStatusHistory> RoomStatusHistory => Set<RoomStatusHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // utc timestamps
    protected override void ConfigureConventions(ModelConfigurationBuilder cb)
    {
        cb.Properties<DateTime>().HaveConversion<UtcConverter>();
        cb.Properties<DateTime?>().HaveConversion<NullableUtcConverter>();
    }

    public class UtcConverter : ValueConverter<DateTime, DateTime>
    {
        public UtcConverter() : base(v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc)) { }
    }

    public class NullableUtcConverter : ValueConverter<DateTime?, DateTime?>
    {
        public NullableUtcConverter() : base(
            v => v.HasValue && v.Value.Kind == DateTimeKind.Local ? v.Value.ToUniversalTime() : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v) { }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasIndex(x => x.Username).IsUnique();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
        });

        b.Entity<Room>(e =>
        {
            e.ToTable("Rooms", t =>
            {
                t.HasCheckConstraint("CK_Rooms_Floor", "`Floor` >= 0");
            });
            e.HasIndex(x => x.RoomNumber).IsUnique();
            e.HasIndex(x => new { x.ServiceStatus, x.OccupancyStatus, x.HousekeepingStatus });
            e.HasIndex(x => x.RoomType);
            e.Property(x => x.OccupancyStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.HousekeepingStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ServiceStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.LastUpdatedAt).HasColumnType("datetime(6)");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Ignore(x => x.DisplayStatus);
        });

        b.Entity<HousekeepingNotice>(e =>
        {
            e.ToTable("HousekeepingNotices");
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedAt).HasColumnType("datetime(6)");
            e.Property(x => x.AcknowledgedAt).HasColumnType("datetime(6)");
            e.Property(x => x.CompletedAt).HasColumnType("datetime(6)");
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.RoomId);
            e.HasIndex(x => x.AssignedToId);
            e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedTo).WithMany().HasForeignKey(x => x.AssignedToId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<RoomStatusHistory>(e =>
        {
            e.ToTable("RoomStatusHistory");
            e.Property(x => x.PreviousStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.NewStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ChangedAt).HasColumnType("datetime(6)");
            e.HasIndex(x => new { x.RoomId, x.ChangedAt });
            e.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ChangedBy).WithMany().HasForeignKey(x => x.ChangedById).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AuditLog>(e =>
        {
            e.ToTable("AuditLogs");
            e.Property(x => x.Timestamp).HasColumnType("datetime(6)");
            e.HasIndex(x => x.Timestamp);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        foreach (var entry in ChangeTracker.Entries<IVersioned>())
            if (entry.State == EntityState.Modified)
                entry.Entity.Version++;
        return base.SaveChangesAsync(ct);
    }
}
