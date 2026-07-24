using DispatchSystem.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace DispatchSystem.Api.Data
{
    public class DispatchDbContext(DbContextOptions<DispatchDbContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Rider> Riders => Set<Rider>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Rider>()
                .HasMany(r => r.Orders)
                .WithOne(o => o.Rider)
                .HasForeignKey(o => o.RiderId);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();
        }
    }
}
