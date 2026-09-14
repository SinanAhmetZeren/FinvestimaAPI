using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FinvestimaAPI.Dtos.DcfDtos;

namespace FinvestimaAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DcfController : ControllerBase
    {
        private readonly ILogger<DcfController> _logger;

        public DcfController(ILogger<DcfController> logger)
        {
            _logger = logger;
        }

        [HttpPost("calculate")]
        [RequestSizeLimit(2_000_000)]
        public async Task<ActionResult<DcfResultDto>> Calculate(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                return BadRequest("File must be a .csv file.");

            List<(string Period, decimal CashFlow, decimal DiscountRate)> rows = new();

            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                string? line;
                bool isFirstLine = true;
                int lineNumber = 0;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (isFirstLine)
                    {
                        isFirstLine = false;
                        if (!char.IsDigit(line.Trim().Split(',').ElementAtOrDefault(1)?.FirstOrDefault() ?? 'a'))
                            continue; // skip header row
                    }

                    var parts = line.Split(',');
                    if (parts.Length < 3)
                        return BadRequest($"Line {lineNumber}: expected 3 columns (Period,CashFlow,DiscountRate).");

                    var period = parts[0].Trim();

                    if (!decimal.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cashFlow))
                        return BadRequest($"Line {lineNumber}: invalid CashFlow value '{parts[1]}'.");

                    if (!decimal.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var discountRate))
                        return BadRequest($"Line {lineNumber}: invalid DiscountRate value '{parts[2]}'.");

                    rows.Add((period, cashFlow, discountRate));
                }
            }

            if (rows.Count == 0)
                return BadRequest("CSV contained no data rows.");

            var result = new DcfResultDto();
            decimal cumulativeFactor = 1m;

            foreach (var row in rows)
            {
                cumulativeFactor *= (1 + row.DiscountRate);
                var presentValue = row.CashFlow / cumulativeFactor;

                result.Periods.Add(new DcfPeriodResultDto
                {
                    Period = row.Period,
                    CashFlow = row.CashFlow,
                    DiscountRate = row.DiscountRate,
                    CumulativeDiscountFactor = cumulativeFactor,
                    PresentValue = presentValue,
                });

                result.TotalPresentValue += presentValue;
            }

            _logger.LogInformation("DCF calculated: {PeriodCount} periods, total PV {Total}", rows.Count, result.TotalPresentValue);

            return Ok(result);
        }
    }
}
