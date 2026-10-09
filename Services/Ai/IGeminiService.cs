using FinvestimaAPI.Dtos.ValuatorProDtos;

namespace FinvestimaAPI.Services.Ai
{
    public interface IGeminiService
    {
        /// <summary>
        /// Extracts mizan (trial balance) rows from a newly uploaded file into the
        /// Firma/MizanYil/MizanHesap shape. currentSnapshot is given only as context — the
        /// response must cover only the years/accounts found in the new file, not restate
        /// snapshot data.
        /// </summary>
        Task<MizanExtractionResultDto?> ExtractMizanAsync(byte[] fileBytes, string mimeType, MizanExtractionResultDto? currentSnapshot);
    }
}
