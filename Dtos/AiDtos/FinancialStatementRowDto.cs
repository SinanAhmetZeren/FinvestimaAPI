namespace FinvestimaAPI.Dtos.AiDtos
{
    public class FinancialStatementRowDto
    {
        public string Label { get; set; } = string.Empty;
        public string RowType { get; set; } = "line"; // "title" | "line" | "sum"
        public List<decimal?> Values { get; set; } = new();
    }
}
