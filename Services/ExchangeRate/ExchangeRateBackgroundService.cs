using FinvestimaAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace FinvestimaAPI.Services.ExchangeRate
{
    public class ExchangeRateBackgroundService : BackgroundService
    {
        private static readonly DateOnly BackfillStart = new(2022, 1, 1);
        private static readonly TimeZoneInfo IstanbulTz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExchangeRateBackgroundService> _logger;

        public ExchangeRateBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ExchangeRateBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        private static DateTime IstanbulNow() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, IstanbulTz);

        private static DateTime NextOccurrenceOf(TimeOnly timeOfDay)
        {
            var now = IstanbulNow();
            var candidate = now.Date + timeOfDay.ToTimeSpan();
            return candidate > now ? candidate : candidate.AddDays(1);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RunBackfillAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                var nextRun = NextOccurrenceOf(new TimeOnly(18, 0));
                await DelayUntilAsync(nextRun, stoppingToken);

                var success = await UpdateTodayAsync(stoppingToken);
                if (!success)
                {
                    var retryAt = NextOccurrenceOf(new TimeOnly(20, 0));
                    await DelayUntilAsync(retryAt, stoppingToken);
                    await UpdateTodayAsync(stoppingToken);
                }
            }
        }

        private static async Task DelayUntilAsync(DateTime targetIstanbulTime, CancellationToken ct)
        {
            var delay = targetIstanbulTime - IstanbulNow();
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, ct);
            }
        }

        private async Task RunBackfillAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var tcmb = scope.ServiceProvider.GetRequiredService<ITcmbExchangeRateService>();

            var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
            var hasAnyData = await db.ExchangeRates.AnyAsync(ct);
            var oldestNeeded = hasAnyData
                ? await db.ExchangeRates.MinAsync(r => (DateOnly?)r.Date, ct) ?? BackfillStart
                : BackfillStart;

            if (oldestNeeded > BackfillStart)
            {
                _logger.LogInformation("Backfilling exchange rates from {From} to {To}", BackfillStart, oldestNeeded.AddDays(-1));
                await tcmb.BackfillAsync(BackfillStart, oldestNeeded.AddDays(-1), ct);
            }

            var newestExisting = hasAnyData
                ? await db.ExchangeRates.MaxAsync(r => (DateOnly?)r.Date, ct)
                : null;
            var from = newestExisting.HasValue ? newestExisting.Value.AddDays(1) : BackfillStart;

            if (from <= yesterday)
            {
                _logger.LogInformation("Backfilling exchange rates from {From} to {To}", from, yesterday);
                var inserted = await tcmb.BackfillAsync(from, yesterday, ct);
                _logger.LogInformation("Exchange rate backfill inserted {Count} rows", inserted);
            }
        }

        /// <returns>True if today's rate is present in the database by the end of this run.</returns>
        private async Task<bool> UpdateTodayAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DataContext>();
            var tcmb = scope.ServiceProvider.GetRequiredService<ITcmbExchangeRateService>();

            var today = DateOnly.FromDateTime(IstanbulNow());
            var windowStart = today.AddDays(-30);

            var existingDates = await db.ExchangeRates
                .Where(r => r.Date >= windowStart && r.Date <= today)
                .Select(r => r.Date)
                .ToListAsync(ct);
            var existing = existingDates.ToHashSet();

            var todayStored = existing.Contains(today)
                || today.DayOfWeek == DayOfWeek.Saturday
                || today.DayOfWeek == DayOfWeek.Sunday;

            for (var date = windowStart; date <= today; date = date.AddDays(1))
            {
                if (existing.Contains(date)) continue;
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) continue;
                ct.ThrowIfCancellationRequested();

                var rate = await tcmb.FetchForDateAsync(date, ct);
                if (rate != null)
                {
                    db.ExchangeRates.Add(rate);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Stored exchange rate for {Date}", date);
                    if (date == today) todayStored = true;
                }

                await Task.Delay(200, ct);
            }

            return todayStored;
        }
    }
}
