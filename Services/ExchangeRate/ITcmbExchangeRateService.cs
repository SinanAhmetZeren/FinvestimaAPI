using FinvestimaAPI.Models;

namespace FinvestimaAPI.Services.ExchangeRate
{
    public interface ITcmbExchangeRateService
    {
        Task<Models.ExchangeRate?> FetchForDateAsync(DateOnly date, CancellationToken ct = default);
        Task<int> BackfillAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
    }
}
