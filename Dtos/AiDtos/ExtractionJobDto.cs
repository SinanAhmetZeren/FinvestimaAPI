using FinvestimaAPI.Models;

namespace FinvestimaAPI.Dtos.AiDtos
{
    public class ExtractionJobDto
    {
        public Guid JobId { get; set; }
        public ExtractionJobStatus Status { get; set; }
        public string? ErrorMessage { get; set; }
        public FinancialExtractionResultDto? Result { get; set; }
    }
}
