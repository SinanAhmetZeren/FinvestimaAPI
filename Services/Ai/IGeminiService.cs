using FinvestimaAPI.Dtos.ValuatorProDtos;

namespace FinvestimaAPI.Services.Ai
{
    public interface IGeminiService
    {
        /// <summary>
        /// Extracts trial balance rows from a newly uploaded file into the TrialBalanceYear/
        /// TrialBalanceAccount shape. currentSnapshot is given only as context — the response
        /// must cover only the years/accounts found in the new file, not restate snapshot data.
        /// </summary>
        Task<TrialBalanceExtractionResultDto?> ExtractTrialBalanceAsync(byte[] fileBytes, string mimeType, TrialBalanceExtractionResultDto? currentSnapshot);
    }
}
