using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.AiDtos;
using FinvestimaAPI.Helpers;
using FinvestimaAPI.Models;
using FinvestimaAPI.Services.Ai;

namespace FinvestimaAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AiController : ControllerBase
    {
        private readonly IGeminiService _geminiService;
        private readonly DataContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AiController> _logger;

        public AiController(
            IGeminiService geminiService,
            DataContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<AiController> logger)
        {
            _geminiService = geminiService;
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpPost("analyze-file")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> AnalyzeFile(IFormFile file, [FromForm] string? prompt)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var effectivePrompt = string.IsNullOrWhiteSpace(prompt)
                ? "Describe what is in this file in detail."
                : prompt;

            var result = await _geminiService.AnalyzeFileAsync(bytes, file.ContentType, effectivePrompt);
            if (result == null)
                return StatusCode(502, "Gemini did not return a valid response.");

            return Ok(new { response = result });
        }

        [HttpPost("extract-financials")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ExtractFinancials(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var fileName = file.FileName;

            var mimeType = file.ContentType;
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    mimeType = "text/csv";
                else if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    mimeType = "application/pdf";
            }

            if (fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var text = ExcelTextExtractor.ExtractAsText(bytes, fileName);
                    bytes = System.Text.Encoding.UTF8.GetBytes(text);
                    mimeType = "text/csv";
                    Console.WriteLine($"[AiController] Converted Excel file '{fileName}' to text ({bytes.Length} bytes)");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to parse Excel file {FileName}", fileName);
                    return BadRequest("Could not read the Excel file. Make sure it's a valid .xls or .xlsx file.");
                }
            }

            var job = new FinancialExtractionJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = fileName,
                Status = ExtractionJobStatus.Pending,
            };

            _context.FinancialExtractionJobs.Add(job);
            await _context.SaveChangesAsync();

            Console.WriteLine($"[AiController] Created extraction job {job.Id} for file '{fileName}' ({bytes.Length} bytes)");

            _ = Task.Run(() => ProcessJobAsync(job.Id, bytes, mimeType));

            return Accepted(new { jobId = job.Id });
        }

        [HttpGet("extract-financials/{jobId}")]
        public async Task<ActionResult<ExtractionJobDto>> GetExtractionJob(Guid jobId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var job = await _context.FinancialExtractionJobs
                .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId);

            if (job == null)
                return NotFound();

            return Ok(new ExtractionJobDto
            {
                JobId = job.Id,
                Status = job.Status,
                ErrorMessage = job.ErrorMessage,
                Result = job.ResultJson != null
                    ? JsonSerializer.Deserialize<FinancialExtractionResultDto>(job.ResultJson)
                    : null,
            });
        }

        private async Task ProcessJobAsync(Guid jobId, byte[] fileBytes, string mimeType)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();

            var job = await db.FinancialExtractionJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            job.Status = ExtractionJobStatus.Processing;
            await db.SaveChangesAsync();
            Console.WriteLine($"[AiController] Job {jobId}: processing started");

            try
            {
                var result = await geminiService.ExtractFinancialDataAsync(fileBytes, mimeType);
                if (result == null)
                {
                    job.Status = ExtractionJobStatus.Failed;
                    job.ErrorMessage = "Gemini did not return a valid response.";
                    Console.WriteLine($"[AiController] Job {jobId}: failed — Gemini returned null");
                }
                else
                {
                    job.Status = ExtractionJobStatus.Completed;
                    job.ResultJson = JsonSerializer.Serialize(result);
                    Console.WriteLine($"[AiController] Job {jobId}: completed with {result.Statements.Count} statements");
                }
            }
            catch (Exception ex)
            {
                job.Status = ExtractionJobStatus.Failed;
                job.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Extraction job {JobId} failed", jobId);
                Console.WriteLine($"[AiController] Job {jobId}: failed with exception — {ex.Message}");
            }

            job.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
