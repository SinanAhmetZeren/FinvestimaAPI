using FinvestimaAPI.Dtos.AiDtos;

namespace FinvestimaAPI.Services.Ai
{
    public interface IGeminiService
    {
        Task<string?> AnalyzeFileAsync(byte[] fileBytes, string mimeType, string prompt);
        Task<FinancialExtractionResultDto?> ExtractFinancialDataAsync(byte[] fileBytes, string mimeType);
    }
}
