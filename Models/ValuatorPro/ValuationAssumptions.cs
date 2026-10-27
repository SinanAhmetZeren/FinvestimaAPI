namespace FinvestimaAPI.Models.ValuatorPro
{
    // L5/L6 "Varsayımlar" ve "Değerleme" panellerindeki tüm sürücüler (büyüme, maliyet oranları,
    // yatırım/NİS, vergi, WACC, terminal değer, çarpanlar, ağırlıklar...) — task başına TEK satır,
    // ham JSON olarak saklanır (frontend'deki `asm` objesiyle birebir). Mizan ilk yüklendiğinde
    // hesaplanan varsayılanlarla otomatik doldurulur; elle değişiklikler "Kaydet" ile üzerine yazar.
    public class ValuationAssumptions
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string AssumptionsJson { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
