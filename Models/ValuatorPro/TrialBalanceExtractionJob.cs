namespace FinvestimaAPI.Models.ValuatorPro
{
    // Tracks a single file upload's Gemini extraction (async job + polling) for a ValuationTask.
    public class TrialBalanceExtractionJob
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string FileName { get; set; } = string.Empty;
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
