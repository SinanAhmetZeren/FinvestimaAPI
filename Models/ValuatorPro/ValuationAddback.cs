namespace FinvestimaAPI.Models.ValuatorPro
{
    // L4 "Geri eklemeler" listesi — task başına kalıcı. Kaydet her seferinde TÜM listeyi
    // değiştirir (bkz. controller'daki PUT), tıpkı ComparableMultiplesTableRow gibi.
    public class ValuationAddback
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
        // "TRY" ya da "USD" — USD ise FAVÖK'e eklenmeden önce o yılın ortalama kuruyla TL'ye çevrilir.
        public string Currency { get; set; } = "TRY";
        public bool HasEvidence { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
    }
}
