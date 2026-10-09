namespace FinvestimaAPI.Models.ValuatorPro
{
    public class MizanHesap
    {
        public Guid Id { get; set; }
        public Guid MizanYilId { get; set; }
        public MizanYil? MizanYil { get; set; }

        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
    }
}
