using Khan.Core.Catalog;
using Khan.Core.Orders;
using Microsoft.EntityFrameworkCore;

namespace Khan.Core.Persistence;

public sealed class KhanDbContext : DbContext
{
    public KhanDbContext(DbContextOptions<KhanDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(e =>
        {
            e.HasIndex(p => p.Slug).IsUnique();
            e.Property(p => p.Slug).HasMaxLength(80);
            e.Property(p => p.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.MerchantOrderId).IsUnique();
            e.Property(o => o.MerchantOrderId).HasMaxLength(40);
            e.HasMany(o => o.Items).WithOne().HasForeignKey(i => i.OrderId);
        });
    }
}
