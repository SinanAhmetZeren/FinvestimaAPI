namespace FinvestimaAPI.Dtos.AiDtos
{
    public class FinancialStatementDto
    {
        public string StatementType { get; set; } = string.Empty;
        public List<string> Columns { get; set; } = new();
        public List<FinancialStatementRowDto> Rows { get; set; } = new();
        public List<FinancialFootnoteDto> Footnotes { get; set; } = new();
    }
}
