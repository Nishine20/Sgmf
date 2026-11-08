using Microsoft.EntityFrameworkCore;
using Sgmf.Core;

namespace Sgmf.Api.Data;

public class SgmfDb(DbContextOptions<SgmfDb> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<MaintenancePlan> Plans => Set<MaintenancePlan>();
    public DbSet<UsageReading> Readings => Set<UsageReading>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 2);
        b.Properties<decimal?>().HavePrecision(18, 2);
        b.Properties<Enum>().HaveConversion<string>().HaveMaxLength(30);
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<AppUser>().HasIndex(u => u.UserName).IsUnique();
        m.Entity<Asset>(e =>
        {
            e.HasIndex(a => a.Code).IsUnique();
            e.Property(a => a.Code).HasMaxLength(30);
            e.Ignore(a => a.CurrentKm).Ignore(a => a.CurrentHours);
            e.HasMany(a => a.Plans).WithOne().HasForeignKey(p => p.AssetId);
            e.HasMany(a => a.Readings).WithOne().HasForeignKey(r => r.AssetId);
            e.HasMany(a => a.WorkOrders).WithOne().HasForeignKey(w => w.AssetId);
        });
        m.Entity<WorkOrder>().Ignore(w => w.IsActive);
        m.Entity<WorkOrder>().HasOne<MaintenancePlan>().WithMany().HasForeignKey(w => w.PlanId).OnDelete(DeleteBehavior.NoAction);
    }
}
