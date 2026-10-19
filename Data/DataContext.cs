using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Models;
using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Data
{
    public class DataContext : IdentityDbContext<AppUser>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<ExchangeRate> ExchangeRates { get; set; }
        public DbSet<Company> Companies { get; set; }
        public DbSet<ValuationTask> ValuationTasks { get; set; }
        public DbSet<TrialBalanceYear> TrialBalanceYears { get; set; }
        public DbSet<TrialBalanceAccount> TrialBalanceAccounts { get; set; }
        public DbSet<TrialBalanceExtractionJob> TrialBalanceExtractionJobs { get; set; }
        public DbSet<ComparableMultiplesJob> ComparableMultiplesJobs { get; set; }
        public DbSet<ComparableMultiplesTableRow> ComparableMultiplesTableRows { get; set; }
        public DbSet<MonthlySalesObject> MonthlySalesObjects { get; set; }
        public DbSet<MonthlySalesRow> MonthlySalesRows { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .HasIndex(u => u.PublicId)
                .IsUnique();

            modelBuilder.Entity<ExchangeRate>()
                .HasIndex(r => r.Date)
                .IsUnique();

            modelBuilder.Entity<Company>()
                .HasMany(c => c.ValuationTasks)
                .WithOne(t => t.Company)
                .HasForeignKey(t => t.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ValuationTask>()
                .HasMany(t => t.TrialBalanceYears)
                .WithOne(y => y.ValuationTask)
                .HasForeignKey(y => y.ValuationTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TrialBalanceYear>()
                .HasIndex(y => new { y.ValuationTaskId, y.Year })
                .IsUnique();

            modelBuilder.Entity<TrialBalanceYear>()
                .HasMany(y => y.TrialBalanceAccounts)
                .WithOne(a => a.TrialBalanceYear)
                .HasForeignKey(a => a.TrialBalanceYearId)
                .OnDelete(DeleteBehavior.Cascade);

            // NOT unique on (TrialBalanceYearId, Code): re-uploads add new rows for the same
            // code instead of overwriting (see TrialBalanceAccount.CreatedAt) — version
            // resolution happens at read time, not via a DB constraint.
            modelBuilder.Entity<TrialBalanceAccount>()
                .HasIndex(a => new { a.TrialBalanceYearId, a.Code });

            // Denormalized convenience FK — actual cascade delete happens via TrialBalanceYearId
            // above, so this one is Restrict to avoid EF Core's multiple-cascade-path error.
            modelBuilder.Entity<TrialBalanceAccount>()
                .HasOne(a => a.ValuationTask)
                .WithMany()
                .HasForeignKey(a => a.ValuationTaskId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TrialBalanceExtractionJob>()
                .HasOne(j => j.ValuationTask)
                .WithMany()
                .HasForeignKey(j => j.ValuationTaskId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ValuationTask>()
                .HasOne(t => t.MonthlySalesObject)
                .WithOne(m => m.ValuationTask)
                .HasForeignKey<MonthlySalesObject>(m => m.ValuationTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<MonthlySalesObject>()
                .HasIndex(m => m.ValuationTaskId)
                .IsUnique();

            modelBuilder.Entity<MonthlySalesObject>()
                .HasMany(o => o.MonthlySalesRows)
                .WithOne(r => r.MonthlySalesObject)
                .HasForeignKey(r => r.MonthlySalesObjectId)
                .OnDelete(DeleteBehavior.Cascade);

            // NOT unique on (MonthlySalesObjectId, Year, Month): re-uploads add new rows
            // for the same month instead of overwriting — same versioning pattern as
            // TrialBalanceAccount, latest-by-CreatedAt resolved at read time.
            modelBuilder.Entity<MonthlySalesRow>()
                .HasIndex(r => new { r.MonthlySalesObjectId, r.Year, r.Month });
        }
    }
}
