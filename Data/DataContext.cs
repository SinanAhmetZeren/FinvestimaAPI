using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Models;
using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Data
{
    public class DataContext : IdentityDbContext<AppUser>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<FinancialExtractionJob> FinancialExtractionJobs { get; set; }
        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<Firma> Firmalar { get; set; }
        public DbSet<MizanYil> MizanYillar { get; set; }
        public DbSet<MizanHesap> MizanHesaplar { get; set; }
        public DbSet<MizanExtractionJob> MizanExtractionJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.PublicId)
                .IsUnique();

            modelBuilder.Entity<ExchangeRate>()
                .HasIndex(r => r.Date)
                .IsUnique();

            modelBuilder.Entity<Firma>()
                .HasMany(f => f.MizanYillar)
                .WithOne(m => m.Firma)
                .HasForeignKey(m => m.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MizanYil>()
                .HasIndex(m => new { m.FirmaId, m.Year })
                .IsUnique();

            modelBuilder.Entity<MizanYil>()
                .HasMany(m => m.MizanHesaplar)
                .WithOne(h => h.MizanYil)
                .HasForeignKey(h => h.MizanYilId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MizanHesap>()
                .HasIndex(h => new { h.MizanYilId, h.Code })
                .IsUnique();

            modelBuilder.Entity<MizanExtractionJob>()
                .HasOne(j => j.Firma)
                .WithMany()
                .HasForeignKey(j => j.FirmaId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
