using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FinvestimaAPI.Data;
using FinvestimaAPI.Helpers;
using FinvestimaAPI.Helpers.ValuationEngine;

namespace FinvestimaAPI.Services.ValuatorPro
{
    // L5/L6 varsayımlarını backend motoruyla hesaplayıp kaydeder. Hem mizan yüklenince
    // (TrialBalanceExtractionService içinden, otomatik, frontend'den ayrı bir istek olmadan) hem
    // kullanıcı "Varsayımları baza döndür" dediğinde (ValuatorProController üzerinden) çağrılır.
    public static class ValuationAssumptionsService
    {
        public static async Task<object?> ComputeAsync(DataContext db, Guid valuationTaskId)
        {
            var years = await db.TrialBalanceYears
                .Include(y => y.TrialBalanceAccounts)
                .Where(y => y.ValuationTaskId == valuationTaskId && !y.IsRemoved)
                .ToListAsync();
            if (years.Count == 0) return null;

            var ledgers = years.Select(y =>
            {
                var resolved = TrialBalanceVersionResolver.ResolveLatest(y.TrialBalanceAccounts).Select(r => new RawAccountRow
                {
                    Code = r.Row.Code, Name = r.Row.Name, Debit = r.Row.Debit, Credit = r.Row.Credit,
                    DebitBalance = r.Row.DebitBalance, CreditBalance = r.Row.CreditBalance,
                });
                return LedgerEngine.BuildLedger(resolved, y.Year, y.Months);
            }).ToList();

            var addbacks = await db.ValuationAddbacks.Where(a => a.ValuationTaskId == valuationTaskId).ToListAsync();
            var daOverrides = await db.DepreciationOverrides.Where(d => d.ValuationTaskId == valuationTaskId).ToListAsync();
            var state = new HistoryState
            {
                Addbacks = addbacks.Select(a => new AddbackInput { Year = a.Year, Tl = a.Amount, Currency = a.Currency, On = a.IsActive }).ToList(),
                DaOverrides = daOverrides.Select(d => new DepreciationOverrideInput { Year = d.Year, Amount = d.Amount }).ToList(),
            };

            var hist = StatementsEngine.BuildHistory(ledgers, state);
            return ValuationDefaults.DefaultAssumptions(hist);
        }

        // Hesaplayıp ValuationAssumptions tablosuna yazar. Döner: hesaplanabildiyse sonucun JSON'ı, yoksa null.
        public static async Task<string?> ComputeAndSaveAsync(DataContext db, Guid valuationTaskId)
        {
            var result = await ComputeAsync(db, valuationTaskId);
            if (result == null) return null;

            var json = JsonSerializer.Serialize(result);
            var row = await db.ValuationAssumptions.FirstOrDefaultAsync(r => r.ValuationTaskId == valuationTaskId);
            if (row == null)
            {
                row = new Models.ValuatorPro.ValuationAssumptions { Id = Guid.NewGuid(), ValuationTaskId = valuationTaskId, AssumptionsJson = json };
                db.ValuationAssumptions.Add(row);
            }
            else
            {
                row.AssumptionsJson = json;
                row.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
            return json;
        }
    }
}
