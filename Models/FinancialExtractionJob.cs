namespace FinvestimaAPI.Models
{
    public enum ExtractionJobStatus
    {
        Pending,
        Processing,
        Completed,
        Failed
    }

    public class FinancialExtractionJob
    {
        public Guid Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ResultJson { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
