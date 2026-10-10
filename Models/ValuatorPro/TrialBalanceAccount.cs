namespace FinvestimaAPI.Models.ValuatorPro
{
    public class TrialBalanceAccount
    {
        public Guid Id { get; set; }
        public Guid TrialBalanceYearId { get; set; }
        public TrialBalanceYear? TrialBalanceYear { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
