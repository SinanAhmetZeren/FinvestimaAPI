namespace FinvestimaAPI.Models.ValuatorPro
{
    public class TrialBalanceYear
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public int Year { get; set; }
        public int Months { get; set; } = 12;
        public bool IsPartial { get; set; }
        public decimal? FileDebitTotal { get; set; }
        public decimal? FileCreditTotal { get; set; }
        public string? Label { get; set; }
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public List<TrialBalanceAccount> TrialBalanceAccounts { get; set; } = new();
    }
}
