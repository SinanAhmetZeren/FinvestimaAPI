namespace FinvestimaAPI.Models.ValuatorPro
{
    public class TrialBalanceAccount
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public Guid TrialBalanceYearId { get; set; }
        public TrialBalanceYear? TrialBalanceYear { get; set; }
        // Tekdüzen'de bu kod/isim zaten orijinaldir. GAAP'ta Code/Name motorun
        // kullandığı gölge hesap planına ait kod/isimdir — kullanıcıya ASLA
        // gösterilmez; OriginalCode/OriginalName dolu olduğunda UI onları gösterir.
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? OriginalCode { get; set; }
        public string? OriginalName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public bool IsManualOverride { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Katkı seviyeleri (Statements.jsx) — 7'li gider alt hesapları için B-F sınıflandırması.
        // Null = henüz hesaplanmadı. ContributionClassIsManual true ise kullanıcı elle değiştirmiştir
        // ve yeni mizan yüklemesinde ÜZERİNE YAZILMAZ (bkz. TrialBalanceExtractionService); false ise
        // her yeni yüklemede önceki satırdan taşınır ve frontend'in güncel classify() çıktısıyla
        // senkronize tutulur (bkz. SaveContributionClassifications).
        public string? ContributionClass { get; set; }
        public bool ContributionClassIsManual { get; set; }
    }
}
