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
                    new Rider { Id = 2, IsAvailable = false, Name = "劉備" },
                    new Rider { Id = 3, IsAvailable = false, Name = "關羽" },
                    new Rider { Id = 4, IsAvailable = false, Name = "趙雲" },
                    new Rider { Id = 5, IsAvailable = false, Name = "馬超" },
                    new Rider { Id = 6, IsAvailable = false, Name = "黃忠" },
                    new Rider { Id = 7, IsAvailable = false, Name = "曹操" },
                    new Rider { Id = 8, IsAvailable = false, Name = "孫權" },
                    new Rider { Id = 9, IsAvailable = false, Name = "周瑜" },
                    new Rider { Id = 10, IsAvailable = false, Name = "呂布" }
                );

            modelBuilder.Entity<Rider>()
                .HasMany(r => r.Orders)
                .WithOne(o => o.Rider)
                .HasForeignKey(o => o.RiderId);

            modelBuilder.Entity<Order>()
                .Property(o => o.Status)
                .HasConversion<string>();

            modelBuilder.Entity<Order>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
