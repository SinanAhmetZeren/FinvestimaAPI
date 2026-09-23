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
        private readonly IWebHostEnvironment _env;

        public AiController(
            IGeminiService geminiService,
            DataContext context,
            IServiceScopeFactory scopeFactory,
            ILogger<AiController> logger,
            IWebHostEnvironment env)
        {
            _geminiService = geminiService;
            _context = context;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _env = env;
        }

        // Debug-only: dumps the exact bytes we're about to send to Gemini into ~/Downloads
        // so we can visually confirm what a redaction actually did. Development-only, never
        // runs in production.
        private void DebugSaveToDownloads(string label, byte[] bytes, string extension)
        {
            if (!_env.IsDevelopment()) return;
            try
            {
                var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                var path = Path.Combine(downloads, $"debug-{label}-{DateTime.Now:HHmmss}.{extension}");
                System.IO.File.WriteAllBytes(path, bytes);
                Console.WriteLine($"[AiController] DEBUG: wrote {path} ({bytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AiController] DEBUG: failed to write debug file — {ex.Message}");
            }
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

        private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg", "image/jpg"
        };

        [HttpPost("extract-financials/preview")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ExtractFinancialsPreview(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var fileName = file.FileName;
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var mimeType = ResolveMimeType(file.ContentType, fileName);

            try
            {
                byte[] previewSourceBytes;
                var pageCount = 1;
                if (mimeType == "application/pdf" || fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    previewSourceBytes = LogoRedactionHelper.RasterizePdfFirstPageToPng(bytes);
                    pageCount = LogoRedactionHelper.GetPdfPageCount(bytes);
                }
                else if (ImageMimeTypes.Contains(mimeType) || IsImageFileName(fileName))
                {
                    previewSourceBytes = bytes;
                }
                else
                {
                    return BadRequest("Preview is only supported for PDF and image files.");
                }

                var (base64Png, topPercent, bottomPercent) = LogoRedactionHelper.AnalyzeForPreview(previewSourceBytes);

                return Ok(new LogoPreviewDto
                {
                    PreviewImageBase64 = base64Png,
                    SuggestedTopPercent = topPercent,
                    SuggestedBottomPercent = bottomPercent,
                    PageCount = pageCount,
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to build logo redaction preview for {FileName}", fileName);
                return BadRequest("Could not generate a preview for this file.");
            }
        }

        [HttpPost("extract-financials")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> ExtractFinancials(IFormFile file, [FromForm] string? topPercent, [FromForm] string? bottomPercent, [FromForm] string? leftPercent, [FromForm] string? rightPercent, [FromForm] string? pages, [FromForm] string? documentType, [FromForm] string? years)
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

            var mimeType = ResolveMimeType(file.ContentType, fileName);

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

            // Parse with InvariantCulture explicitly: the server's current culture (e.g. tr-TR,
            // which uses ',' as the decimal separator) would otherwise mis-parse a value like
            // "23.45" sent from the browser as something wildly wrong (e.g. 2345 or worse),
            // which then clamps to 100% and whites out the entire page.
            var top = float.TryParse(topPercent, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var topVal) ? topVal : 0f;
            var bottom = float.TryParse(bottomPercent, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var bottomVal) ? bottomVal : 0f;
            var left = float.TryParse(leftPercent, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var leftVal) ? leftVal : 0f;
            var right = float.TryParse(rightPercent, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var rightVal) ? rightVal : 0f;
            var cropRequested = (top > 0 || bottom > 0 || left > 0 || right > 0) &&
                (mimeType == "application/pdf" || ImageMimeTypes.Contains(mimeType));

            // Page selection ("1-3,5", print-dialog style) only applies to PDFs. A null result
            // from ParsePageSelection means "all pages" — the existing, unfiltered behavior.
            List<int>? selectedPageIndices = null;
            if (mimeType == "application/pdf" && !string.IsNullOrWhiteSpace(pages))
            {
                var pdfPageCount = LogoRedactionHelper.GetPdfPageCount(bytes);
                selectedPageIndices = LogoRedactionHelper.ParsePageSelection(pages, pdfPageCount);
            }
            var pagesRequested = selectedPageIndices != null;

            List<(byte[] Bytes, string MimeType)>? redactedPages = null;

            if (cropRequested || pagesRequested)
            {
                if (mimeType == "application/pdf")
                {
                    var pagePngs = selectedPageIndices != null
                        ? LogoRedactionHelper.RasterizePdfPagesToPng(bytes, selectedPageIndices)
                        : LogoRedactionHelper.RasterizePdfAllPagesToPng(bytes);
                    redactedPages = pagePngs
                        .Select(pagePng => (LogoRedactionHelper.RedactBands(pagePng, top, bottom, left, right), "image/png"))
                        .ToList();
                    Console.WriteLine($"[AiController] Redacted {redactedPages.Count} PDF page(s) for '{fileName}' (top={top}%, bottom={bottom}%, left={left}%, right={right}%, pages={pages ?? "all"})");
                    for (int i = 0; i < redactedPages.Count; i++)
                        DebugSaveToDownloads($"page{i + 1}", redactedPages[i].Bytes, "png");
                }
                else
                {
                    bytes = LogoRedactionHelper.RedactBands(bytes, top, bottom, left, right);
                    mimeType = "image/png";
                    Console.WriteLine($"[AiController] Redacted image bands for '{fileName}' (top={top}%, bottom={bottom}%, left={left}%, right={right}%)");
                    DebugSaveToDownloads("image", bytes, "png");
                }
            }

            var job = new FinancialExtractionJob
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                FileName = fileName,
                DocumentType = string.IsNullOrWhiteSpace(documentType) ? null : documentType,
                Years = string.IsNullOrWhiteSpace(years) ? null : years,
                Status = ExtractionJobStatus.Pending,
            };

            _context.FinancialExtractionJobs.Add(job);
            await _context.SaveChangesAsync();

            Console.WriteLine($"[AiController] Created extraction job {job.Id} for file '{fileName}' ({bytes.Length} bytes)");

            if (redactedPages != null)
            {
                _ = Task.Run(() => ProcessJobFromPagesAsync(job.Id, redactedPages));
            }
            else
            {
                _ = Task.Run(() => ProcessJobAsync(job.Id, bytes, mimeType));
            }

            return Accepted(new { jobId = job.Id });
        }

        private static string ResolveMimeType(string? contentType, string fileName)
        {
            var mimeType = contentType;
            if (string.IsNullOrWhiteSpace(mimeType) || mimeType == "application/octet-stream")
            {
                if (fileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    mimeType = "text/csv";
                else if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    mimeType = "application/pdf";
                else if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                    mimeType = "image/png";
                else if (fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase))
                    mimeType = "image/jpeg";
            }
            return mimeType ?? "application/octet-stream";
        }

        private static bool IsImageFileName(string fileName) =>
            fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);

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

        private async Task ProcessJobFromPagesAsync(Guid jobId, List<(byte[] Bytes, string MimeType)> pages)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var geminiService = scope.ServiceProvider.GetRequiredService<IGeminiService>();

            var job = await db.FinancialExtractionJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return;

            job.Status = ExtractionJobStatus.Processing;
            await db.SaveChangesAsync();
            Console.WriteLine($"[AiController] Job {jobId}: processing started ({pages.Count} redacted page image(s))");

            try
            {
                var result = await geminiService.ExtractFinancialDataFromPagesAsync(pages);
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
