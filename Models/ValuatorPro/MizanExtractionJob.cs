namespace FinvestimaAPI.Models.ValuatorPro
{
    // Tek bir dosya yüklemesinin Gemini işleminin takibi (polling) için.
    // Hangi yıl(lar)ı kapsadığı Gemini cevap verene kadar bilinmediğinden
    // MizanYil'e değil, doğrudan Firma'ya bağlanır.
    public class MizanExtractionJob
    {
        public Guid Id { get; set; }
        public Guid FirmaId { get; set; }
        public Firma? Firma { get; set; }

        public string FileName { get; set; } = string.Empty;
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
