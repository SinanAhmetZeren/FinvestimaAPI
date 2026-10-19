namespace FinvestimaAPI.Models.ValuatorPro
{
    // L6 "Emsal çarpanlar" tablosu — artık hardcoded SECTOR_MULTIPLES değil, task başına boş
    // başlayan ve kullanıcının (AI popup'tan "Kullan" ile ya da elle girip "Kaydet" ile)
    // doldurduğu, DB'de saklanan bir liste. Kaydet her seferinde TÜM tabloyu değiştirir (bkz.
    // controller'daki PUT) — satır bazlı CRUD yok, UI zaten tüm satırları birlikte yönetiyor.
    public class ComparableMultiplesTableRow
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal RangeLow { get; set; }
        public decimal RangeHigh { get; set; }
        public decimal Median { get; set; }
        public string? Vintage { get; set; }
        public string? SourceBasis { get; set; }
        public string? SourceUrl { get; set; }
        public bool IsEstimate { get; set; }
        public int SortOrder { get; set; }
    }
}
