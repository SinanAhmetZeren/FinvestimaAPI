using FinvestimaAPI.Dtos.ValuatorProDtos;

namespace FinvestimaAPI.Helpers
{
    public class AggregatedTrialBalanceAccount
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public bool DepthMismatch { get; set; }
        public bool IsManualOverride { get; set; }

        // True when a code under this root has multiple versions (from different uploads —
        // see TrialBalanceVersionResolver). Set by the caller, not by Aggregate itself.
        public bool VersionConflict { get; set; }
    }

    // Gemini'den dönen ham mizan satırları birden fazla derinlikte (102, 102 01, 102 01 01 ...)
    // gelebilir. Bu sınıf her 3'lü kök için her derinliği KENDİ İÇİNDE toplayıp bir "102" adayı
    // üretir, adayları birbirleriyle karşılaştırır ve en az derinlikteki değeri nihai sonuç olarak
    // kullanır. Tutmuyorsa DepthMismatch=true işaretlenir. Sonuç SADECE 3'lü (kök) seviyede döner —
    // alt derinlikler DB'ye/UI'a hiç yansımaz.
    public static class TrialBalanceDepthAggregator
    {
        private const decimal ToleranceAbsolute = 1m;
        private const decimal ToleranceRelative = 0.0005m;

        public static List<AggregatedTrialBalanceAccount> Aggregate(List<TrialBalanceAccountDto> rawRows)
        {
            var byRoot = new Dictionary<string, List<(int Depth, TrialBalanceAccountDto Row)>>();

            foreach (var row in rawRows)
            {
                var parts = row.Code.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) continue;
                var root = parts[0];
                var depth = parts.Length;

                if (!byRoot.TryGetValue(root, out var list))
                    byRoot[root] = list = new();
                list.Add((depth, row));
            }

            var result = new List<AggregatedTrialBalanceAccount>();

            foreach (var (root, rows) in byRoot)
            {
                var candidates = rows
                    .GroupBy(r => r.Depth)
                    .OrderBy(g => g.Key)
                    .Select(g => new
                    {
                        Depth = g.Key,
                        Debit = g.Sum(x => x.Row.Debit),
                        Credit = g.Sum(x => x.Row.Credit),
                        DebitBalance = g.Sum(x => x.Row.DebitBalance),
                        CreditBalance = g.Sum(x => x.Row.CreditBalance),
                        Name = g.First().Row.Name,
                        IsManualOverride = g.Any(x => x.Row.IsManualOverride),
                    })
                    .ToList();

                var chosen = candidates.First(); // en az derinlik
                var mismatch = candidates.Any(c =>
                    !WithinTolerance(c.Debit, chosen.Debit) ||
                    !WithinTolerance(c.Credit, chosen.Credit));

                result.Add(new AggregatedTrialBalanceAccount
                {
                    Code = root,
                    Name = chosen.Name,
                    Debit = chosen.Debit,
                    Credit = chosen.Credit,
                    DebitBalance = chosen.DebitBalance,
                    CreditBalance = chosen.CreditBalance,
                    DepthMismatch = mismatch,
                    IsManualOverride = chosen.IsManualOverride,
                });
            }

            return result;
        }

        private static bool WithinTolerance(decimal a, decimal b)
        {
            var diff = Math.Abs(a - b);
            var tolerance = Math.Max(ToleranceAbsolute, Math.Abs(b) * ToleranceRelative);
            return diff <= tolerance;
        }
    }
}
