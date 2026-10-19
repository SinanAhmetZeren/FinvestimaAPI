using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Helpers
{
    public class GaapAccountSource
    {
        public string? OriginalCode { get; set; }
        public string? OriginalName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
    }

    public class GaapAccountGroup
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public bool IsManualOverride { get; set; }
        public List<GaapAccountSource> Sources { get; set; } = new();
    }

    // Tekdüzen'in derinlik problemi (aynı hesap "102"/"102 01" gibi birden fazla derinlikte)
    // GAAP'ta yok — gölge kodlar zaten düz 3'lü. Buradaki durum farklı: aynı yüklemede FARKLI
    // orijinal hesaplar (örn. iki ayrı banka hesabı) aynı gölge koda eşlenebilir. Bu yüzden
    // TrialBalanceVersionResolver/TrialBalanceDepthAggregator yerine: önce HER ORİJİNAL HESABIN
    // (OriginalCode) en güncel satırı seçilir — gölge koduna bakılmaksızın, tüm veri setinde
    // tek seferde. Bu, bir orijinal hesabın iki yükleme arasında farklı bir gölge koda yeniden
    // sınıflandırılması durumunu da kapsar: eski satır kendi (artık öksüz) gölge kodunda
    // "en güncel" sayılıp hayatta kalamaz, çünkü dedup gölge koduna göre değil baştan
    // OriginalCode'a göre yapılıyor. Sadece ondan SONRA, deduplicate edilmiş satırlar güncel
    // gölge koduna göre gruplanıp toplanır.
    public static class GaapAccountGrouper
    {
        public static List<GaapAccountGroup> Group(IEnumerable<TrialBalanceAccount> rows)
        {
            var latestPerOriginal = rows
                .GroupBy(r => r.OriginalCode ?? r.Id.ToString())
                .Select(g => g.OrderByDescending(r => r.CreatedAt).First());

            return latestPerOriginal
                .GroupBy(r => r.Code)
                .Select(g => new GaapAccountGroup
                {
                    Code = g.Key,
                    Name = g.First().Name,
                    Debit = g.Sum(r => r.Debit),
                    Credit = g.Sum(r => r.Credit),
                    DebitBalance = g.Sum(r => r.DebitBalance),
                    CreditBalance = g.Sum(r => r.CreditBalance),
                    IsManualOverride = g.Any(r => r.IsManualOverride),
                    Sources = g.Select(r => new GaapAccountSource
                    {
                        OriginalCode = r.OriginalCode,
                        OriginalName = r.OriginalName,
                        Debit = r.Debit,
                        Credit = r.Credit,
                        DebitBalance = r.DebitBalance,
                        CreditBalance = r.CreditBalance,
                    }).ToList(),
                })
                .ToList();
        }
    }
}
