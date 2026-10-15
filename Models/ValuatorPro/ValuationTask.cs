namespace FinvestimaAPI.Models.ValuatorPro
{
    public class ValuationTask
    {
        public Guid Id { get; set; }
        public Guid CompanyId { get; set; }
        public Company? Company { get; set; }
        public string Title { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<TrialBalanceYear> TrialBalanceYears { get; set; } = new();
        public MonthlySalesObject? MonthlySalesObject { get; set; }
    }
}
