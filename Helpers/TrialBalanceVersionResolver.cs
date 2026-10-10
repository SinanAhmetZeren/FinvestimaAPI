using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Helpers
{
    public class ResolvedTrialBalanceAccount
    {
        public TrialBalanceAccountDto Row { get; set; } = new();
        public bool HasMultipleVersions { get; set; }
    }

    // Yeniden yükleme eski satırların üzerine yazmaz, yeni satır ekler — bir kod için birden
    // fazla CreatedAt zamanına ait satır birikebilir. Bu sınıf, koda göre (isme göre değil) en
    // son (en güncel CreatedAt'e sahip) satırı seçer; bir kod için birden fazla FARKLI zamanlı
    // satır varsa (aynı job'dan gelenler aynı CreatedAt'i paylaştığı için gerçek bir çakışma
    // sayılmaz) HasMultipleVersions=true işaretler.
    public static class TrialBalanceVersionResolver
    {
        public static List<ResolvedTrialBalanceAccount> ResolveLatest(IEnumerable<TrialBalanceAccount> rows)
        {
            return rows
                .GroupBy(r => r.Code)
                .Select(g =>
                {
                    var latest = g.OrderByDescending(r => r.CreatedAt).First();
                    var distinctTimestamps = g.Select(r => r.CreatedAt).Distinct().Count();
                    return new ResolvedTrialBalanceAccount
                    {
                        Row = new TrialBalanceAccountDto
                        {
                            Code = latest.Code,
                            Name = latest.Name,
                            Debit = latest.Debit,
                            Credit = latest.Credit,
                            DebitBalance = latest.DebitBalance,
                            CreditBalance = latest.CreditBalance,
                        },
                        HasMultipleVersions = distinctTimestamps > 1,
                    };
                })
                .ToList();
        }
    }
}
