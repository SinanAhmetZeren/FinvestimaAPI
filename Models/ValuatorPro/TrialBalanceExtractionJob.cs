namespace FinvestimaAPI.Models.ValuatorPro
{
    // Tracks a single file upload's Gemini extraction (async job + polling). Used both for
    // mizan (trial balance) uploads, which always have a ValuationTaskId, and for generic
    // financial-statement/PDF extraction (AiController), where it's optional (the old,
    // now-unreachable Extract.jsx page never had a task).
    public class TrialBalanceExtractionJob
    {
        public Guid Id { get; set; }
        public Guid? ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string? UserId { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string? DocumentType { get; set; }
        public string? Years { get; set; }
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? ResultJson { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
