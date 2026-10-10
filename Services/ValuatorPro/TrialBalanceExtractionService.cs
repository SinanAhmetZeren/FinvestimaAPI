using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Helpers;
using FinvestimaAPI.Models;
using FinvestimaAPI.Models.ValuatorPro;
using FinvestimaAPI.Services.Ai;

namespace FinvestimaAPI.Services.ValuatorPro
{
    public class TrialBalanceExtractionService : ITrialBalanceExtractionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<TrialBalanceExtractionService> _logger;

        public TrialBalanceExtractionService(IServiceScopeFactory scopeFactory, ILogger<TrialBalanceExtractionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessJobAsync(Guid jobId, Guid valuationTaskId, byte[] fileBytes, string mimeType)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();

            var job = await db.TrialBalanceExtractionJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            job.Status = ExtractionJobStatus.Processing;
            await db.SaveChangesAsync();

            try
            {
                var task = await db.ValuationTasks.Include(t => t.TrialBalanceYears).ThenInclude(y => y.TrialBalanceAccounts)
                    .FirstOrDefaultAsync(t => t.Id == valuationTaskId);
                if (task == null) throw new InvalidOperationException("ValuationTask not found.");

                var snapshot = BuildSnapshot(task);

                var result = await geminiService.ExtractTrialBalanceAsync(fileBytes, mimeType, snapshot);
                if (result == null || result.TrialBalanceYears.Count == 0)
                {
                    job.Status = ExtractionJobStatus.Failed;
                    job.ErrorMessage = "Gemini did not return any recognizable trial balance data.";
                }
                else
                {
                    var writtenYears = await WriteYearsAsync(db, valuationTaskId, result);
                    job.Status = ExtractionJobStatus.Completed;
                    job.ResultJson = JsonSerializer.Serialize(new { years = writtenYears });
                }

                job.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                if (ex is DbUpdateConcurrencyException concurrencyEx)
                {
                    foreach (var entry in concurrencyEx.Entries)
                        _logger.LogError("Concurrency conflict on entity {EntityType} with state {State}: {Values}", entry.Entity.GetType().Name, entry.State, JsonSerializer.Serialize(entry.CurrentValues.ToObject()));
                }
                _logger.LogError(ex, "Trial balance extraction job {JobId} failed", jobId);
                try
                {
                    job.Status = ExtractionJobStatus.Failed;
                    job.ErrorMessage = ex.Message;
                    job.CompletedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    _logger.LogError(saveEx, "Failed to persist failure state for job {JobId}", jobId);
                }
            }
        }

        private static TrialBalanceExtractionResultDto BuildSnapshot(ValuationTask task)
        {
            return new TrialBalanceExtractionResultDto
            {
                // Gemini'ye bağlam olarak gönderilen snapshot, her kod için en güncel versiyonu
                // yansıtmalı — geçmiş yüklemelerden kalan eski satırları tekrarlamamalı.
                TrialBalanceYears = task.TrialBalanceYears.Select(y => new TrialBalanceYearDto
                {
                    Year = y.Year,
                    Months = y.Months,
                    IsPartial = y.IsPartial,
                    FileDebitTotal = y.FileDebitTotal,
                    FileCreditTotal = y.FileCreditTotal,
                    Label = y.Label,
                    TrialBalanceAccounts = TrialBalanceVersionResolver.ResolveLatest(y.TrialBalanceAccounts).Select(r => r.Row).ToList(),
                }).ToList(),
            };
        }

        private static async Task<List<object>> WriteYearsAsync(DataContext db, Guid valuationTaskId, TrialBalanceExtractionResultDto result)
        {
            var writtenYears = new List<object>();
            var uploadedAt = DateTime.UtcNow; // bu job'da yazılan tüm satırlar aynı zamanı paylaşır

            foreach (var yearDto in result.TrialBalanceYears)
            {
                // Gemini'den dönen TÜM satırlar (hangi derinlikte olursa olsun) olduğu gibi DB'ye
                // yazılır — hiçbir şey atılmaz/kümülenmez. 3'lü seviyeye kümüleme ve mismatch
                // kontrolü, veriyi değiştirmeyen AYRI bir hesaplama (bkz. TrialBalanceDepthAggregator)
                // — sadece doğrulama/raporlama amaçlı kullanılır, burada sadece bilgi amaçlı çalışır.
                var aggregated = TrialBalanceDepthAggregator.Aggregate(yearDto.TrialBalanceAccounts);

                var debitSum = aggregated.Sum(h => h.Debit);
                var creditSum = aggregated.Sum(h => h.Credit);
                var balanced = Math.Abs(debitSum - creditSum) <= Math.Max(1m, debitSum * 0.0005m);

                var existing = await db.TrialBalanceYears.Include(y => y.TrialBalanceAccounts)
                    .FirstOrDefaultAsync(y => y.ValuationTaskId == valuationTaskId && y.Year == yearDto.Year);

                if (existing == null)
                {
                    existing = new TrialBalanceYear { Id = Guid.NewGuid(), ValuationTaskId = valuationTaskId, Year = yearDto.Year };
                    db.TrialBalanceYears.Add(existing);
                }

                existing.Months = yearDto.Months;
                existing.IsPartial = yearDto.IsPartial;
                existing.FileDebitTotal = yearDto.FileDebitTotal;
                existing.FileCreditTotal = yearDto.FileCreditTotal;
                existing.Label = yearDto.Label;
                existing.Status = balanced ? ExtractionJobStatus.Completed : ExtractionJobStatus.Failed;
                existing.ErrorMessage = balanced ? null : $"Borç toplamı ({debitSum}) alacak toplamı ({creditSum}) ile eşleşmiyor.";

                // Eski satırların üzerine yazılmaz — yeni satırlar eklenir. Koda göre hangisinin
                // güncel olduğu okuma anında (TrialBalanceVersionResolver) CreatedAt'e bakılarak belirlenir.
                foreach (var h in yearDto.TrialBalanceAccounts)
                {
                    db.TrialBalanceAccounts.Add(new TrialBalanceAccount
                    {
                        Id = Guid.NewGuid(),
                        TrialBalanceYearId = existing.Id,
                        Code = h.Code,
                        Name = h.Name,
                        Debit = h.Debit,
                        Credit = h.Credit,
                        DebitBalance = h.DebitBalance,
                        CreditBalance = h.CreditBalance,
                        CreatedAt = uploadedAt,
                    });
                }

                writtenYears.Add(new
                {
                    year = yearDto.Year,
                    status = existing.Status.ToString(),
                    errorMessage = existing.ErrorMessage,
                    accountCount = yearDto.TrialBalanceAccounts.Count,
                    // 3'lü seviyede kümülenmiş sonuç (hesaplama amaçlı) — derinlikler arası tutmayan kodlar.
                    depthMismatches = aggregated.Where(a => a.DepthMismatch).Select(a => a.Code).ToList(),
                });
            }

            return writtenYears;
        }
    }
}
