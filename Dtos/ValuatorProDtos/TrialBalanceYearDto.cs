namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    public class TrialBalanceYearDto
    {
        public int Year { get; set; }
        public int Months { get; set; } = 12;
        public bool IsPartial { get; set; }
        public decimal? FileDebitTotal { get; set; }
        public decimal? FileCreditTotal { get; set; }
        public string? Label { get; set; }
        public List<TrialBalanceAccountDto> TrialBalanceAccounts { get; set; } = new();
    }
}
