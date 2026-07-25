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
                .HasData(
                    new Rider { Id = 1, IsAvailable = true, Name = "張飛" },
                    new Rider { Id = 2, IsAvailable = false, Name = "劉備" }
                );

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
