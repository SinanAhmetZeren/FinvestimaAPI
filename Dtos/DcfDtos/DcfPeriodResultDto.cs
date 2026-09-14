namespace FinvestimaAPI.Dtos.DcfDtos
{
    public class DcfPeriodResultDto
    {
        public string Period { get; set; } = string.Empty;
        public decimal CashFlow { get; set; }
        public decimal DiscountRate { get; set; }
        public decimal CumulativeDiscountFactor { get; set; }
        public decimal PresentValue { get; set; }
    }
}
