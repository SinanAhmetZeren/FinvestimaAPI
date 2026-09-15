namespace FinvestimaAPI.Dtos.AiDtos
{
    public class FinancialLineItemDto
    {
        public string StatementType { get; set; } = string.Empty;
        public string LineItem { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty;
        public decimal? Value { get; set; }
    }
}
