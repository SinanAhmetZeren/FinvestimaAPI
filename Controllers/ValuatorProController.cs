using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Helpers;
using FinvestimaAPI.Models;
using FinvestimaAPI.Models.ValuatorPro;
using FinvestimaAPI.Services.ValuatorPro;

namespace FinvestimaAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ValuatorProController : ControllerBase
    {
        private readonly IMizanExtractionService _mizanExtractionService;
        private readonly DataContext _context;
        private readonly ILogger<ValuatorProController> _logger;

        public ValuatorProController(IMizanExtractionService mizanExtractionService, DataContext context, ILogger<ValuatorProController> logger)
        {
            _mizanExtractionService = mizanExtractionService;
            _context = context;
            _logger = logger;
        }

        // Dosyayı alır, bir MizanExtractionJob açar ve hemen jobId döner. Gemini çağrısı ve DB
        // yazımı arka planda (MizanExtractionService) yapılır.
        [HttpPost("mizan-yukle")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> MizanYukle(IFormFile file, [FromForm] Guid? firmaId, [FromForm] string? firmaAdi)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            var firma = firmaId.HasValue
                ? await _context.Firmalar.FirstOrDefaultAsync(f => f.Id == firmaId.Value && f.UserId == userId)
                : null;

            if (firmaId.HasValue && firma == null)
                return NotFound("Firma bulunamadı.");

            if (firma == null)
            {
                firma = new Firma { Id = Guid.NewGuid(), Ad = firmaAdi ?? "Yeni Firma", UserId = userId };
                _context.Firmalar.Add(firma);
            }

            var fileName = file.FileName;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var mimeType = file.ContentType;

            if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var text = ExcelTextExtractor.ExtractAsText(bytes, fileName);
                    text = MizanDepthFilter.RemoveDeepestSubAccounts(text);
                    bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    mimeType = "text/csv";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Excel file {FileName}", fileName);
                    return BadRequest("Could not read the Excel file. Make sure it's a valid .xls or .xlsx file.");
                }
            }
            else if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                mimeType = fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? "text/csv" : "application/pdf";
            }

            var job = new MizanExtractionJob { Id = Guid.NewGuid(), FirmaId = firma.Id, FileName = fileName, Status = ExtractionJobStatus.Pending };
            _context.MizanExtractionJobs.Add(job);
            await _context.SaveChangesAsync();

            _ = Task.Run(() => _mizanExtractionService.ProcessJobAsync(job.Id, firma.Id, bytes, mimeType));

            return Accepted(new { jobId = job.Id, firmaId = firma.Id });
        }

        [HttpGet("mizan-sorgula/{jobId}")]
        public async Task<IActionResult> MizanSorgula(Guid jobId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var job = await _context.MizanExtractionJobs.Include(j => j.Firma)
                .FirstOrDefaultAsync(j => j.Id == jobId && j.Firma!.UserId == userId);

            if (job == null)
                return NotFound();

            return Ok(new
            {
                jobId = job.Id,
                firmaId = job.FirmaId,
                status = job.Status.ToString(),
                errorMessage = job.ErrorMessage,
                result = job.ResultJson != null ? JsonSerializer.Deserialize<JsonElement>(job.ResultJson) : (JsonElement?)null,
            });
        }

        [HttpGet("firmabilgileri/{firmaId}")]
        public async Task<IActionResult> FirmaBilgileri(Guid firmaId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var firma = await _context.Firmalar
                .Include(f => f.MizanYillar).ThenInclude(y => y.MizanHesaplar)
                .FirstOrDefaultAsync(f => f.Id == firmaId && f.UserId == userId);

            if (firma == null)
                return NotFound();

            return Ok(new
            {
                firma.Id,
                firma.Ad,
                years = firma.MizanYillar.Select(y =>
                {
                    // DB'de ham (her derinlikteki) satırlar duruyor; 3'lü seviyeye kümüleme ve
                    // mismatch kontrolü burada, okuma anında, veriyi değiştirmeden yapılır.
                    var raw = y.MizanHesaplar.Select(h => new MizanHesapDto
                    {
                        Code = h.Code,
                        Name = h.Name,
                        Debit = h.Debit,
                        Credit = h.Credit,
                        DebitBalance = h.DebitBalance,
                        CreditBalance = h.CreditBalance,
                    }).ToList();
                    var aggregated = MizanDepthAggregator.Aggregate(raw);

                    return new
                    {
                        y.Year,
                        y.Months,
                        y.IsPartial,
                        Status = y.Status.ToString(),
                        y.ErrorMessage,
                        accounts = aggregated.Select(a => new { a.Code, a.Name, a.Debit, a.Credit, a.DebitBalance, a.CreditBalance, a.DepthMismatch }),
                    };
                }),
            });
        }
    }
}
