namespace FinvestimaAPI.Models.ValuatorPro
{
    // L6 "Emsal çarpanlar" AI araştırması — Gemini'nin arama+yapılandırma adımları birlikte
    // 50s+ sürebiliyor, tek bir senkron HTTP isteği istemci tarafı zaman aşımına (ve proxy/LB
    // zaman aşımlarına) takılıyordu. TrialBalanceExtractionJob ile aynı async job + polling
    // deseni kullanılıyor.
    public class ComparableMultiplesJob
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public string? UserId { get; set; }
        public string SectorSubSector { get; set; } = string.Empty;
        public decimal YearlySales { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public decimal Ebitda { get; set; }
        public string Language { get; set; } = "en";
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
