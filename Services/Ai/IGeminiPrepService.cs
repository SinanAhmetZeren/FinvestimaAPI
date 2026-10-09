using FinvestimaAPI.Dtos.AiDtos;

namespace FinvestimaAPI.Services.Ai
{
    // Önceden var olan, genel mali tablo (gelir tablosu/bilanço/nakit akış) extraction akışı —
    // ValuatorPro'nun mizan extraction'ından (IGeminiService) bağımsız, AiController tarafından kullanılır.
    public interface IGeminiPrepService
    {
        Task<string?> AnalyzeFileAsync(byte[] fileBytes, string mimeType, string prompt);
        Task<FinancialExtractionResultDto?> ExtractFinancialDataAsync(byte[] fileBytes, string mimeType);

        /// <summary>
        /// Same extraction as ExtractFinancialDataAsync, but for a document represented as an
        /// ordered set of page images (e.g. redacted per-page PNGs rasterized from a PDF) instead
        /// of a single file blob. Each entry becomes its own inlineData part, in order.
        /// </summary>
        Task<FinancialExtractionResultDto?> ExtractFinancialDataFromPagesAsync(IReadOnlyList<(byte[] Bytes, string MimeType)> pages);
    }
}
