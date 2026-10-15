using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly ITrialBalanceExtractionService _trialBalanceExtractionService;
        private readonly DataContext _context;
        private readonly ILogger<ValuatorProController> _logger;
        private readonly UserManager<AppUser> _userManager;

        public ValuatorProController(ITrialBalanceExtractionService trialBalanceExtractionService, DataContext context, ILogger<ValuatorProController> logger, UserManager<AppUser> userManager)
        {
            _trialBalanceExtractionService = trialBalanceExtractionService;
            _context = context;
            _logger = logger;
            _userManager = userManager;
        }

        // ── Admin: müşteriye (ödeme/fatura teyit edildikten sonra) hesap + task açar ──────────
        public class AdminCreateUserRequest
        {
            public string Email { get; set; } = string.Empty;
            public string UserName { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/users")]
        public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest("Email, UserName ve Password zorunlu.");

            var normalizedEmail = _userManager.NormalizeEmail(req.Email.Trim());
            var exists = await _userManager.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail);
            if (exists) return BadRequest("Bu email ile zaten bir kullanıcı var.");

            var user = new AppUser
            {
                Email = req.Email.Trim(),
                UserName = req.UserName.Trim(),
                EmailConfirmed = true,
                Confirmed = true,
                PublicId = Guid.NewGuid().ToString("N")[..10],
            };
            var result = await _userManager.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok(new { user.Id, user.UserName, user.Email });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/users")]
        public async Task<IActionResult> AdminListUsers()
        {
            var users = await _userManager.Users
                .OrderBy(u => u.UserName)
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();
            return Ok(users);
        }

        public class AdminCreateTaskRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string? TaskTitle { get; set; }
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("admin/valuation-tasks")]
        public async Task<IActionResult> AdminCreateTask([FromBody] AdminCreateTaskRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.UserId) || string.IsNullOrWhiteSpace(req.CompanyName))
                return BadRequest("UserId ve CompanyName zorunlu.");

            var targetUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == req.UserId);
            if (targetUser == null) return NotFound("Kullanıcı bulunamadı.");

            var company = new Company { Id = Guid.NewGuid(), Name = req.CompanyName };
            _context.Companies.Add(company);

            var task = new ValuationTask { Id = Guid.NewGuid(), CompanyId = company.Id, Title = req.TaskTitle ?? "Değerleme", UserId = req.UserId };
            _context.ValuationTasks.Add(task);

            await _context.SaveChangesAsync();
            return Ok(new { task.Id, task.Title, companyName = company.Name });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/valuation-tasks")]
        public async Task<IActionResult> AdminListTasks()
        {
            var tasks = await _context.ValuationTasks
                .Include(t => t.Company)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Title, t.UserId, t.CreatedAt, companyName = t.Company!.Name })
                .ToListAsync();
            return Ok(tasks);
        }

        // Müşteri giriş yapınca kendisine atanmış task'ları burada görür — kendisi task açmaz.
        [HttpGet("my-valuation-tasks")]
        public async Task<IActionResult> GetMyValuationTasks()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var tasks = await _context.ValuationTasks
                .Include(t => t.Company)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Title, companyName = t.Company!.Name })
                .ToListAsync();
            return Ok(tasks);
        }

        // Dosyayı alır, bir TrialBalanceExtractionJob açar ve hemen jobId döner. Gemini çağrısı
        // ve DB yazımı arka planda (TrialBalanceExtractionService) yapılır.
        // valuationTaskId verilirse o işe yüklenir; verilmezse companyId (+ yeni bir task) ya da
        // companyName (+ yeni company + yeni task) ile yeni bir ValuationTask açılır.
        [HttpPost("upload-trial-balance")]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadTrialBalance(IFormFile file, [FromForm] Guid? valuationTaskId, [FromForm] Guid? companyId, [FromForm] string? companyName, [FromForm] string? taskTitle, [FromForm] int? maxCodeDigits)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized();

            ValuationTask? task = valuationTaskId.HasValue
                ? await _context.ValuationTasks.FirstOrDefaultAsync(t => t.Id == valuationTaskId.Value)
                : null;

            if (valuationTaskId.HasValue && task == null)
                return NotFound("ValuationTask bulunamadı.");

            if (task == null)
            {
                var company = companyId.HasValue
                    ? await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId.Value)
                    : null;

                if (companyId.HasValue && company == null)
                    return NotFound("Company bulunamadı.");

                if (company == null)
                {
                    company = new Company { Id = Guid.NewGuid(), Name = companyName ?? "Yeni Firma" };
                    _context.Companies.Add(company);
                }

                task = new ValuationTask { Id = Guid.NewGuid(), CompanyId = company.Id, Title = taskTitle ?? "Değerleme", UserId = userId };
                _context.ValuationTasks.Add(task);
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
                    text = MizanDepthFilter.RemoveDeepestSubAccounts(text, maxCodeDigits ?? 7);
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

            var job = new TrialBalanceExtractionJob { Id = Guid.NewGuid(), ValuationTaskId = task.Id, FileName = fileName, Status = ExtractionJobStatus.Pending };
            _context.TrialBalanceExtractionJobs.Add(job);
            await _context.SaveChangesAsync();

            _ = Task.Run(() => _trialBalanceExtractionService.ProcessJobAsync(job.Id, task.Id, bytes, mimeType));

            return Accepted(new { jobId = job.Id, valuationTaskId = task.Id });
        }

        [HttpGet("upload-trial-balance/{jobId}")]
        public async Task<IActionResult> GetUploadJob(Guid jobId)
        {
            var job = await _context.TrialBalanceExtractionJobs.FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return NotFound();

            return Ok(new
            {
                jobId = job.Id,
                valuationTaskId = job.ValuationTaskId,
                status = job.Status.ToString(),
                errorMessage = job.ErrorMessage,
                result = job.ResultJson != null ? JsonSerializer.Deserialize<JsonElement>(job.ResultJson) : (JsonElement?)null,
            });
        }

        public class ManualEditRequest
        {
            public Guid ValuationTaskId { get; set; }
            public int Year { get; set; }
            public string Code { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        // Kullanıcının mizan ızgarasında elle düzelttiği bir hesabı yeni bir satır olarak ekler —
        // eski satırların üzerine yazmaz, tıpkı yeniden yükleme gibi (bkz. TrialBalanceAccount.CreatedAt).
        // En son CreatedAt kazandığı için bu satır otomatik güncel sayılır; sonraki bir dosya
        // yüklemesi bunu bilerek ezebilir (kilitli değil).
        [HttpPost("trial-balance-accounts/manual-edit")]
        public async Task<IActionResult> ManualEdit([FromBody] ManualEditRequest req)
        {
            _logger.LogWarning("[ManualEdit] received valuationTaskId={ValuationTaskId} year={Year} code={Code}", req.ValuationTaskId, req.Year, req.Code);
            var year = await _context.TrialBalanceYears
                .FirstOrDefaultAsync(y => y.ValuationTaskId == req.ValuationTaskId && y.Year == req.Year);
            if (year == null)
            {
                _logger.LogWarning("[ManualEdit] no TrialBalanceYear found for valuationTaskId={ValuationTaskId} year={Year}", req.ValuationTaskId, req.Year);
                return NotFound("TrialBalanceYear bulunamadı.");
            }

            var net = req.Debit - req.Credit;
            var row = new TrialBalanceAccount
            {
                Id = Guid.NewGuid(),
                ValuationTaskId = req.ValuationTaskId,
                TrialBalanceYearId = year.Id,
                Code = req.Code,
                Name = req.Name,
                Debit = req.Debit,
                Credit = req.Credit,
                DebitBalance = Math.Max(net, 0),
                CreditBalance = Math.Max(-net, 0),
                IsManualOverride = true,
                CreatedAt = DateTime.UtcNow,
            };
            _context.TrialBalanceAccounts.Add(row);
            await _context.SaveChangesAsync();

            return Ok(new { row.Id, row.CreatedAt });
        }

        // Bir yılı task'tan tamamen kaldırır (soft-delete) — hücre bazlı düzeltmeden farklı,
        // yanlış/istenmeyen bir yükleme tamamen geri çekilmek istendiğinde kullanılır.
        [HttpPost("valuation-tasks/{valuationTaskId}/years/{year}/remove")]
        public async Task<IActionResult> RemoveYear(Guid valuationTaskId, int year)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ty = await _context.TrialBalanceYears
                .Include(y => y.ValuationTask)
                .FirstOrDefaultAsync(y => y.ValuationTaskId == valuationTaskId && y.Year == year);
            if (ty == null) return NotFound();
            if (ty.ValuationTask?.UserId != userId) return Forbid();

            ty.IsRemoved = true;
            await _context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("valuation-tasks/{valuationTaskId}")]
        public async Task<IActionResult> GetValuationTask(Guid valuationTaskId)
        {
            var task = await _context.ValuationTasks
                .Include(t => t.Company)
                .Include(t => t.TrialBalanceYears).ThenInclude(y => y.TrialBalanceAccounts)
                .FirstOrDefaultAsync(t => t.Id == valuationTaskId);

            if (task == null)
                return NotFound();

            return Ok(new
            {
                task.Id,
                task.Title,
                companyId = task.CompanyId,
                companyName = task.Company?.Name,
                years = task.TrialBalanceYears.Where(y => !y.IsRemoved).Select(y =>
                {
                    // DB'de ham (her derinlikteki, her yüklemeden) satırlar duruyor. Okuma anında:
                    // 1) koda göre en güncel versiyon seçilir (eski yüklemelerin üzerine yazılmaz,
                    //    sadece hangisi gösterileceğine burada karar verilir),
                    // 2) 3'lü seviyeye kümülenir ve derinlik mismatch kontrolü yapılır.
                    var resolved = TrialBalanceVersionResolver.ResolveLatest(y.TrialBalanceAccounts);
                    var conflictRoots = resolved.Where(r => r.HasMultipleVersions)
                        .Select(r => r.Row.Code.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0])
                        .ToHashSet();

                    var aggregated = TrialBalanceDepthAggregator.Aggregate(resolved.Select(r => r.Row).ToList());
                    foreach (var a in aggregated)
                        if (conflictRoots.Contains(a.Code)) a.VersionConflict = true;

                    return new
                    {
                        y.Year,
                        y.Months,
                        y.IsPartial,
                        y.FileName,
                        Status = y.Status.ToString(),
                        y.ErrorMessage,
                        accounts = aggregated.Select(a => new { a.Code, a.Name, a.Debit, a.Credit, a.DebitBalance, a.CreditBalance, a.DepthMismatch, a.VersionConflict, a.IsManualOverride }),
                    };
                }),
            });
        }
    }
}
