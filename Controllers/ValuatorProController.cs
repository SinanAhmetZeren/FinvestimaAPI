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
using FinvestimaAPI.Services.Ai;

namespace FinvestimaAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ValuatorProController : ControllerBase
    {
        private readonly ITrialBalanceExtractionService _trialBalanceExtractionService;
        private readonly IComparableMultiplesJobService _comparableMultiplesJobService;
        private readonly DataContext _context;
        private readonly ILogger<ValuatorProController> _logger;
        private readonly UserManager<AppUser> _userManager;

        public ValuatorProController(ITrialBalanceExtractionService trialBalanceExtractionService, IComparableMultiplesJobService comparableMultiplesJobService, DataContext context, ILogger<ValuatorProController> logger, UserManager<AppUser> userManager)
        {
            _trialBalanceExtractionService = trialBalanceExtractionService;
            _comparableMultiplesJobService = comparableMultiplesJobService;
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
            public string Language { get; set; } = "tr";
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
                Language = string.IsNullOrWhiteSpace(req.Language) ? "tr" : req.Language.Trim(),
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
                .Select(u => new { u.Id, u.UserName, u.Email, u.Language })
                .ToListAsync();
            return Ok(users);
        }

        public class AdminUpdateUserLanguageRequest
        {
            public string Language { get; set; } = "tr";
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("admin/users/{userId}/language")]
        public async Task<IActionResult> AdminUpdateUserLanguage(string userId, [FromBody] AdminUpdateUserLanguageRequest req)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound();
            user.Language = string.IsNullOrWhiteSpace(req.Language) ? "tr" : req.Language.Trim();
            await _userManager.UpdateAsync(user);
            return Ok(new { user.Id, user.Language });
        }

        public class AdminCreateTaskRequest
        {
            public string UserId { get; set; } = string.Empty;
            public string CompanyName { get; set; } = string.Empty;
            public string? TaskTitle { get; set; }
            public AccountingStandard AccountingStandard { get; set; } = AccountingStandard.Tekduzen;
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

            var task = new ValuationTask { Id = Guid.NewGuid(), CompanyId = company.Id, Title = req.TaskTitle ?? "Değerleme", UserId = req.UserId, AccountingStandard = req.AccountingStandard };
            _context.ValuationTasks.Add(task);

            await _context.SaveChangesAsync();
            return Ok(new { task.Id, task.Title, companyName = company.Name, accountingStandard = task.AccountingStandard.ToString() });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("admin/valuation-tasks")]
        public async Task<IActionResult> AdminListTasks()
        {
            var tasks = await _context.ValuationTasks
                .Include(t => t.Company)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => new { t.Id, t.Title, t.UserId, t.CreatedAt, companyName = t.Company!.Name, accountingStandard = t.AccountingStandard.ToString() })
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
                .Select(t => new { t.Id, t.Title, companyName = t.Company!.Name, accountingStandard = t.AccountingStandard.ToString() })
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
            // GAAP: düzeltilen hesabın gölge kodu Code alanına, hangi orijinal hesabın
            // düzeltildiği buraya gelir — boşsa Tekdüzen akışı (bkz. GaapAccountGrouper).
            public string? OriginalCode { get; set; }
            public string? OriginalName { get; set; }
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
                OriginalCode = req.OriginalCode,
                OriginalName = req.OriginalName,
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

        // L6'daki "Emsal çarpanlar" tablosunu Gemini ile üretir. Arama+yapılandırma 50s+ sürebiliyor
        // (bkz. GeminiService.GetComparableMultiplesAsync) — tek senkron istek yerine, mizan
        // yüklemesiyle aynı async job + polling deseni kullanılıyor.
        [HttpPost("comparable-multiples")]
        public async Task<IActionResult> StartComparableMultiples([FromBody] ComparableMultiplesRequestDto req)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var job = new ComparableMultiplesJob
            {
                Id = Guid.NewGuid(),
                ValuationTaskId = req.ValuationTaskId,
                UserId = userId,
                SectorSubSector = req.SectorSubSector,
                YearlySales = req.YearlySales,
                Region = req.Region,
                Country = req.Country,
                Ebitda = req.Ebitda,
                Language = req.Language,
                Status = ExtractionJobStatus.Pending,
            };
            _context.ComparableMultiplesJobs.Add(job);
            await _context.SaveChangesAsync();

            _ = Task.Run(() => _comparableMultiplesJobService.ProcessJobAsync(job.Id));

            return Accepted(new { jobId = job.Id });
        }

        [HttpGet("comparable-multiples/{jobId}")]
        public async Task<IActionResult> GetComparableMultiplesJob(Guid jobId)
        {
            var job = await _context.ComparableMultiplesJobs.FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null)
                return NotFound();

            return Ok(new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                errorMessage = job.ErrorMessage,
                result = job.ResultJson != null ? JsonSerializer.Deserialize<JsonElement>(job.ResultJson) : (JsonElement?)null,
            });
        }

        // Sayfa yenilendiğinde son tamamlanmış araştırmayı geri göstermek için — jobId bilmeye
        // gerek yok, task'a göre en son Completed job'u döner (yoksa result:null).
        [HttpGet("valuation-tasks/{valuationTaskId}/comparable-multiples/latest")]
        public async Task<IActionResult> GetLatestComparableMultiplesJob(Guid valuationTaskId)
        {
            var job = await _context.ComparableMultiplesJobs
                .Where(j => j.ValuationTaskId == valuationTaskId && j.Status == ExtractionJobStatus.Completed)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync();

            if (job == null)
                return Ok(new { jobId = (Guid?)null, result = (JsonElement?)null });

            return Ok(new
            {
                jobId = job.Id,
                result = job.ResultJson != null ? JsonSerializer.Deserialize<JsonElement>(job.ResultJson) : (JsonElement?)null,
            });
        }

        // L6 Emsal çarpanlar tablosu — task başına kalıcı, boş başlar. "Kullan"/elle girilen
        // satırlar sadece UI'da tutulur, "Kaydet" ile PUT edilince burada TÜM tablo değiştirilir.
        [HttpGet("valuation-tasks/{valuationTaskId}/comparable-multiples-table")]
        public async Task<IActionResult> GetComparableMultiplesTable(Guid valuationTaskId)
        {
            var rows = await _context.ComparableMultiplesTableRows
                .Where(r => r.ValuationTaskId == valuationTaskId)
                .OrderBy(r => r.SortOrder)
                .Select(r => new { r.Id, r.Category, r.RangeLow, r.RangeHigh, r.Median, r.Vintage, r.SourceBasis, r.SourceUrl, r.IsEstimate })
                .ToListAsync();
            return Ok(rows);
        }

        [HttpPut("valuation-tasks/{valuationTaskId}/comparable-multiples-table")]
        public async Task<IActionResult> SaveComparableMultiplesTable(Guid valuationTaskId, [FromBody] List<ComparableMultiplesTableRowDto> rows)
        {
            var task = await _context.ValuationTasks.FirstOrDefaultAsync(t => t.Id == valuationTaskId);
            if (task == null)
                return NotFound("ValuationTask bulunamadı.");

            var existing = await _context.ComparableMultiplesTableRows.Where(r => r.ValuationTaskId == valuationTaskId).ToListAsync();
            _context.ComparableMultiplesTableRows.RemoveRange(existing);

            var newRows = rows.Select((r, i) => new ComparableMultiplesTableRow
            {
                Id = Guid.NewGuid(),
                ValuationTaskId = valuationTaskId,
                Category = r.Category,
                RangeLow = r.RangeLow,
                RangeHigh = r.RangeHigh,
                Median = r.Median,
                Vintage = r.Vintage,
                SourceBasis = r.SourceBasis,
                SourceUrl = r.SourceUrl,
                IsEstimate = r.IsEstimate,
                SortOrder = i,
            }).ToList();
            _context.ComparableMultiplesTableRows.AddRange(newRows);

            await _context.SaveChangesAsync();
            return Ok(newRows.Select(r => new { r.Id, r.Category, r.RangeLow, r.RangeHigh, r.Median, r.Vintage, r.SourceBasis, r.SourceUrl, r.IsEstimate }));
        }

        public class ReassignGaapAccountRequest
        {
            // Düzeltilecek hesabı orijinal kod/isimle buluruz (gölge kodla değil — kullanıcı
            // gölge kodu hiç görmüyor). newCode, kullanıcının GAAP kalem isimlerinden seçtiği
            // karşılığın gölge kodudur (frontend bu çeviriyi yapıp gönderir).
            public string? OriginalCode { get; set; }
            public string? OriginalName { get; set; }
            public string NewCode { get; set; } = string.Empty;
        }

        // GAAP'ta Gemini'nin yanlış sınıflandırdığı bir hesabı kullanıcının elle düzeltmesi —
        // o yılda, verilen orijinal kod/isme sahip EN GÜNCEL satır(lar)ı (GaapAccountGrouper'ın
        // "current" kümesiyle aynı mantık) bulur, gölge kodunu değiştirir.
        [HttpPost("valuation-tasks/{valuationTaskId}/years/{year}/gaap-accounts/reassign")]
        public async Task<IActionResult> ReassignGaapAccount(Guid valuationTaskId, int year, [FromBody] ReassignGaapAccountRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.NewCode)) return BadRequest("NewCode zorunlu.");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ty = await _context.TrialBalanceYears
                .Include(y => y.ValuationTask)
                .Include(y => y.TrialBalanceAccounts)
                .FirstOrDefaultAsync(y => y.ValuationTaskId == valuationTaskId && y.Year == year);
            if (ty == null) return NotFound();
            if (ty.ValuationTask?.UserId != userId) return Forbid();
            if (ty.ValuationTask?.AccountingStandard != AccountingStandard.UsGaap) return BadRequest("Bu endpoint sadece GAAP task'ları için geçerli.");

            var matches = ty.TrialBalanceAccounts
                .Where(a => a.OriginalCode == req.OriginalCode && a.OriginalName == req.OriginalName)
                .ToList();
            if (matches.Count == 0) return NotFound("Belirtilen orijinal hesap bulunamadı.");

            var latestCreatedAt = matches.Max(a => a.CreatedAt);
            var current = matches.Where(a => a.CreatedAt == latestCreatedAt).ToList();
            foreach (var a in current)
            {
                a.Code = req.NewCode;
                a.IsManualOverride = true;
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = current.Count });
        }

        [HttpGet("valuation-tasks/{valuationTaskId}")]
        public async Task<IActionResult> GetValuationTask(Guid valuationTaskId)
        {
            var task = await _context.ValuationTasks
                .Include(t => t.Company)
                .Include(t => t.TrialBalanceYears).ThenInclude(y => y.TrialBalanceAccounts)
                .Include(t => t.MonthlySalesObject).ThenInclude(o => o!.MonthlySalesRows)
                .FirstOrDefaultAsync(t => t.Id == valuationTaskId);

            if (task == null)
                return NotFound();

            return Ok(new
            {
                task.Id,
                task.Title,
                companyId = task.CompanyId,
                companyName = task.Company?.Name,
                accountingStandard = task.AccountingStandard.ToString(),
                years = task.TrialBalanceYears.Where(y => !y.IsRemoved).Select(y =>
                {
                    if (task.AccountingStandard == AccountingStandard.UsGaap)
                    {
                        // GAAP'ta derinlik yok — gölge koda göre grupla, aynı yüklemedeki farklı
                        // orijinal hesapları topla, UI/düzeltme için orijinal kaynakları sakla.
                        var groups = GaapAccountGrouper.Group(y.TrialBalanceAccounts);
                        return new
                        {
                            y.Year,
                            y.Months,
                            y.IsPartial,
                            y.FileName,
                            Status = y.Status.ToString(),
                            y.ErrorMessage,
                            accounts = groups.Select(g => new { g.Code, g.Name, g.Debit, g.Credit, g.DebitBalance, g.CreditBalance, DepthMismatch = false, VersionConflict = false, g.IsManualOverride, sources = (object?)g.Sources.Select(s => new { s.OriginalCode, s.OriginalName, s.Debit, s.Credit, s.DebitBalance, s.CreditBalance }) }),
                            subAccounts = (object?)null,
                        };
                    }

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

                    // Katkı seviyeleri (Statements.jsx) 6/7 sınıfı hesapların 3 haneden derin kırılımını
                    // kullanır — accounts her zaman 3'lü kök seviyesinde olduğu için bu derin satırları
                    // ayrı bir alanda, ham haliyle (kümelenmeden) gönderiyoruz.
                    var subAccounts = resolved
                        .Where(r => r.Row.Code.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length > 1
                            && (r.Row.Code.TrimStart().StartsWith("6") || r.Row.Code.TrimStart().StartsWith("7")))
                        .Select(r => r.Row);

                    return new
                    {
                        y.Year,
                        y.Months,
                        y.IsPartial,
                        y.FileName,
                        Status = y.Status.ToString(),
                        y.ErrorMessage,
                        accounts = aggregated.Select(a => new { a.Code, a.Name, a.Debit, a.Credit, a.DebitBalance, a.CreditBalance, a.DepthMismatch, a.VersionConflict, a.IsManualOverride, sources = (object?)null }),
                        subAccounts = (object?)subAccounts.Select(r => new { r.Code, r.Name, r.Debit, r.Credit, r.DebitBalance, r.CreditBalance }),
                    };
                }),
                monthlySales = (task.MonthlySalesObject != null && !task.MonthlySalesObject.IsRemoved)
                    ? task.MonthlySalesObject.MonthlySalesRows
                        // Aynı (Year, Month) için birden fazla yükleme olabilir — en güncel (CreatedAt) kazanır.
                        .GroupBy(r => (r.Year, r.Month))
                        .Select(g => g.OrderByDescending(r => r.CreatedAt).First())
                        .OrderBy(r => r.Year).ThenBy(r => r.Month)
                        .Select(r => new { r.Year, r.Month, r.Currency, r.Revenue, r.MaterialCost, r.Opex, r.IsManualOverride })
                    : null,
            });
        }
    }
}
