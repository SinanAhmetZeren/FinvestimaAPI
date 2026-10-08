using System.Globalization;
using System.Xml.Linq;
using FinvestimaAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FinvestimaAPI.Services.ExchangeRate
{
    public class TcmbExchangeRateService : ITcmbExchangeRateService
    {
        private readonly HttpClient _http;
        private readonly DataContext _db;
        private readonly ILogger<TcmbExchangeRateService> _logger;

        public TcmbExchangeRateService(HttpClient http, DataContext db, ILogger<TcmbExchangeRateService> logger)
        {
            _http = http;
            _db = db;
            _logger = logger;
        }

        private static string BuildUrl(DateOnly date)
        {
            var yyyymm = date.ToString("yyyyMM", CultureInfo.InvariantCulture);
            var ddmmyyyy = date.ToString("ddMMyyyy", CultureInfo.InvariantCulture);
            return $"https://www.tcmb.gov.tr/kurlar/{yyyymm}/{ddmmyyyy}.xml";
        }

        public async Task<Models.ExchangeRate?> FetchForDateAsync(DateOnly date, CancellationToken ct = default)
        {
            var url = BuildUrl(date);
            HttpResponseMessage response;
            try
            {
                response = await _http.GetAsync(url, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("TCMB request failed for {Date}: {Message}", date, ex.Message);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                // Weekends / holidays: TCMB has no publication for that date.
                return null;
            }

            var xml = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(xml))
            {
                return null;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch (System.Xml.XmlException ex)
            {
                _logger.LogWarning("TCMB XML parse failed for {Date}: {Message}", date, ex.Message);
                return null;
            }

            decimal ReadDecimal(XElement currency, string elementName)
            {
                var text = currency.Element(elementName)?.Value;
                if (string.IsNullOrWhiteSpace(text) || text == "-") return 0m;
                return decimal.Parse(text, CultureInfo.InvariantCulture);
            }

            var currencies = doc.Descendants("Currency").ToList();
            var usd = currencies.FirstOrDefault(c => (string?)c.Attribute("Kod") == "USD" || (string?)c.Attribute("CurrencyCode") == "USD");
            var eur = currencies.FirstOrDefault(c => (string?)c.Attribute("Kod") == "EUR" || (string?)c.Attribute("CurrencyCode") == "EUR");
            var gbp = currencies.FirstOrDefault(c => (string?)c.Attribute("Kod") == "GBP" || (string?)c.Attribute("CurrencyCode") == "GBP");

            if (usd == null || eur == null || gbp == null)
            {
                _logger.LogWarning("TCMB XML for {Date} missing one of USD/EUR/GBP currency nodes", date);
                return null;
            }

            return new Models.ExchangeRate
            {
                Date = date,
                UsdTryForexBuying = ReadDecimal(usd, "ForexBuying"),
                UsdTryForexSelling = ReadDecimal(usd, "ForexSelling"),
                EurTryForexBuying = ReadDecimal(eur, "ForexBuying"),
                EurTryForexSelling = ReadDecimal(eur, "ForexSelling"),
                GbpTryForexBuying = ReadDecimal(gbp, "ForexBuying"),
                GbpTryForexSelling = ReadDecimal(gbp, "ForexSelling"),
                // TCMB leaves EUR's own CrossRateUSD empty and puts the EUR/USD cross rate in CrossRateOther instead.
                EurUsdCrossRate = ReadDecimal(eur, "CrossRateUSD") != 0m
                    ? ReadDecimal(eur, "CrossRateUSD")
                    : ReadDecimal(eur, "CrossRateOther"),
            };
        }

        public async Task<int> BackfillAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
        {
            var existingDates = await _db.ExchangeRates
                .Where(r => r.Date >= from && r.Date <= to)
                .Select(r => r.Date)
                .ToListAsync(ct);
            var existing = existingDates.ToHashSet();

            var inserted = 0;
            for (var date = from; date <= to; date = date.AddDays(1))
            {
                if (existing.Contains(date)) continue;
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;

                var rate = await FetchForDateAsync(date, ct);
                if (rate != null)
                {
                    _db.ExchangeRates.Add(rate);
                    inserted++;
                    if (inserted % 50 == 0)
                    {
                        await _db.SaveChangesAsync(ct);
                    }
                }

                // Be polite to TCMB's servers; avoid hammering with back-to-back requests.
                await Task.Delay(200, ct);
            }

            await _db.SaveChangesAsync(ct);
            return inserted;
        }
    }
}
