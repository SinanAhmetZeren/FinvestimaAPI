using FinvestimaAPI.Data;
using FinvestimaAPI.Dtos.ExchangeRateDtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinvestimaAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ExchangeRateController : ControllerBase
    {
        private readonly DataContext _db;

        public ExchangeRateController(DataContext db)
        {
            _db = db;
        }

        [HttpGet("fx-summary")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<ActionResult<List<FxYearDto>>> FxSummary()
        {
            var rates = await _db.ExchangeRates
                .OrderBy(r => r.Date)
                .Select(r => new { r.Date, r.UsdTryForexBuying, r.UsdTryForexSelling })
                .ToListAsync();

            var currentYear = DateOnly.FromDateTime(DateTime.UtcNow).Year;

            var result = rates
                .GroupBy(r => r.Date.Year)
                .Select(g =>
                {
                    var mids = g.Select(r => (r.UsdTryForexBuying + r.UsdTryForexSelling) / 2m).ToList();
                    var last = g.OrderBy(r => r.Date).Last();
                    return new FxYearDto
                    {
                        Year = g.Key,
                        Avg = mids.Count > 0 ? Math.Round(mids.Average(), 4) : null,
                        End = Math.Round((last.UsdTryForexBuying + last.UsdTryForexSelling) / 2m, 4),
                        Partial = g.Key == currentYear,
                    };
                })
                .OrderBy(r => r.Year)
                .ToList();

            return Ok(result);
        }
    }
}
