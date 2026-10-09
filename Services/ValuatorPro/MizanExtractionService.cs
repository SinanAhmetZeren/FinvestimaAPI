using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Models;
using FinvestimaAPI.Models.ValuatorPro;
using FinvestimaAPI.Services.Ai;

namespace FinvestimaAPI.Services.ValuatorPro
{
    public class MizanExtractionService : IMizanExtractionService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MizanExtractionService> _logger;

        public MizanExtractionService(IServiceScopeFactory scopeFactory, ILogger<MizanExtractionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessJobAsync(Guid jobId, Guid firmaId, byte[] fileBytes, string mimeType)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();

            var job = await db.MizanExtractionJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            job.Status = ExtractionJobStatus.Processing;
            await db.SaveChangesAsync();

            try
            {
                var firma = await db.Firmalar.Include(f => f.MizanYillar).ThenInclude(y => y.MizanHesaplar)
                    .FirstOrDefaultAsync(f => f.Id == firmaId);
                if (firma == null) throw new InvalidOperationException("Firma not found.");

                var snapshot = BuildSnapshot(firma);

                var result = await geminiService.ExtractMizanAsync(fileBytes, mimeType, snapshot);
                if (result == null || result.MizanYillar.Count == 0)
                {
                    job.Status = ExtractionJobStatus.Failed;
                    job.ErrorMessage = "Gemini did not return any recognizable mizan data.";
                }
                else
                {
                    var writtenYears = await WriteYearsAsync(db, firmaId, result);
                    job.Status = ExtractionJobStatus.Completed;
                    job.ResultJson = JsonSerializer.Serialize(new { years = writtenYears });
                }
            }
            catch (Exception ex)
            {
                job.Status = ExtractionJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Mizan extraction job {JobId} failed", jobId);
            }

            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        private static MizanExtractionResultDto BuildSnapshot(Firma firma)
        {
            return new MizanExtractionResultDto
            {
                MizanYillar = firma.MizanYillar.Select(y => new MizanYilDto
                {
                    Year = y.Year,
                    Months = y.Months,
                    IsPartial = y.IsPartial,
                    FileDebitTotal = y.FileDebitTotal,
                    FileCreditTotal = y.FileCreditTotal,
                    Label = y.Label,
                    MizanHesaplar = y.MizanHesaplar.Select(h => new MizanHesapDto
                    {
                        Code = h.Code,
                        Name = h.Name,
                        Debit = h.Debit,
                        Credit = h.Credit,
                        DebitBalance = h.DebitBalance,
                        CreditBalance = h.CreditBalance,
                    }).ToList(),
                }).ToList(),
            };
        }

        private static async Task<List<object>> WriteYearsAsync(DataContext db, Guid firmaId, MizanExtractionResultDto result)
        {
            var writtenYears = new List<object>();

            foreach (var yearDto in result.MizanYillar)
            {
                // Gemini'den dönen TÜM satırlar (hangi derinlikte olursa olsun) olduğu gibi DB'ye
                // yazılır — hiçbir şey atılmaz/kümülenmez. 3'lü seviyeye kümüleme ve mismatch
                // kontrolü, veriyi değiştirmeyen AYRI bir hesaplama (bkz. MizanDepthAggregator) —
                // sadece doğrulama/raporlama amaçlı kullanılır, burada sadece bilgi amaçlı çalışır.
                var aggregated = MizanDepthAggregator.Aggregate(yearDto.MizanHesaplar);

                var debitSum = aggregated.Sum(h => h.Debit);
                var creditSum = aggregated.Sum(h => h.Credit);
                var balanced = Math.Abs(debitSum - creditSum) <= Math.Max(1m, debitSum * 0.0005m);

                var existing = await db.MizanYillar.Include(y => y.MizanHesaplar)
                    .FirstOrDefaultAsync(y => y.FirmaId == firmaId && y.Year == yearDto.Year);

                if (existing != null)
                {
                    db.MizanHesaplar.RemoveRange(existing.MizanHesaplar);
                }
                else
                {
                    existing = new MizanYil { Id = Guid.NewGuid(), FirmaId = firmaId, Year = yearDto.Year };
                    db.MizanYillar.Add(existing);
                }

                existing.Months = yearDto.Months;
                existing.IsPartial = yearDto.IsPartial;
                existing.FileDebitTotal = yearDto.FileDebitTotal;
                existing.FileCreditTotal = yearDto.FileCreditTotal;
                existing.Label = yearDto.Label;
                existing.Status = balanced ? ExtractionJobStatus.Completed : ExtractionJobStatus.Failed;
                existing.ErrorMessage = balanced ? null : $"Borç toplamı ({debitSum}) alacak toplamı ({creditSum}) ile eşleşmiyor.";
                existing.MizanHesaplar = yearDto.MizanHesaplar.Select(h => new MizanHesap
                {
                    Id = Guid.NewGuid(),
                    Code = h.Code,
                    Name = h.Name,
                    Debit = h.Debit,
                    Credit = h.Credit,
                    DebitBalance = h.DebitBalance,
                    CreditBalance = h.CreditBalance,
                }).ToList();

                writtenYears.Add(new
                {
                    year = yearDto.Year,
                    status = existing.Status.ToString(),
                    errorMessage = existing.ErrorMessage,
                    accountCount = existing.MizanHesaplar.Count,
                    // 3'lü seviyede kümülenmiş sonuç (hesaplama amaçlı) — derinlikler arası tutmayan kodlar.
                    depthMismatches = aggregated.Where(a => a.DepthMismatch).Select(a => a.Code).ToList(),
                });
            }

            return writtenYears;
        }
    }
}
