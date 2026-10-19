using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Models;
using FinvestimaAPI.Services.Ai;

namespace FinvestimaAPI.Services.ValuatorPro
{
    public class ComparableMultiplesJobService : IComparableMultiplesJobService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ComparableMultiplesJobService> _logger;
        // ResultJson olduğu gibi (JsonElement) frontend'e dönüyor — ASP.NET'in controller'lar için
        // ayarladığı camelCase policy'si bu metni dokunmadan geçirir, o yüzden burada ayrıca
        // belirtilmesi gerekiyor (aksi halde C# PascalCase alan adları (Rows, Category...) frontend'in
        // beklediği camelCase (rows, category...) ile uyuşmaz).
        private static readonly JsonSerializerOptions CamelCaseOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public ComparableMultiplesJobService(IServiceScopeFactory scopeFactory, ILogger<ComparableMultiplesJobService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessJobAsync(Guid jobId)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();

            var job = await db.ComparableMultiplesJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            job.Status = ExtractionJobStatus.Processing;
            await db.SaveChangesAsync();

            try
            {
                var rows = await geminiService.GetComparableMultiplesAsync(new ComparableMultiplesRequestDto
                {
                    SectorSubSector = job.SectorSubSector,
                    YearlySales = job.YearlySales,
                    Region = job.Region,
                    Country = job.Country,
                    Ebitda = job.Ebitda,
                    Language = job.Language,
                });

                if (rows == null)
                {
                    job.Status = ExtractionJobStatus.Failed;
                    job.ErrorMessage = "Gemini'den geçerli bir yanıt alınamadı.";
                }
                else
                {
                    // req.Ebitda bin USD cinsinden (V.m2.basis) — M USD için 1000'e bölünür.
                    foreach (var r in rows)
                        r.ImpliedEquityValueM = Math.Round(r.Median * job.Ebitda / 1000m, 1);

                    job.ResultJson = JsonSerializer.Serialize(new ComparableMultiplesResultDto { Rows = rows }, CamelCaseOptions);
                    job.Status = ExtractionJobStatus.Completed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ComparableMultiplesJob {JobId} failed", jobId);
                job.Status = ExtractionJobStatus.Failed;
                job.ErrorMessage = ex.Message;
            }

            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
