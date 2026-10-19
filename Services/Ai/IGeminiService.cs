using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Services.Ai
{
    public interface IGeminiService
    {
        /// <summary>
        /// Extracts trial balance rows from a newly uploaded file into the TrialBalanceYear/
        /// TrialBalanceAccount shape. currentSnapshot is given only as context — the response
        /// must cover only the years/accounts found in the new file, not restate snapshot data.
        /// accountingStandard decides which chart-of-accounts reference is sent: Tekduzen's own
        /// codes are used as-is; UsGaap maps the file's own (non-standard) codes/names onto the
        /// shadow US-GAAP chart while preserving the original code/name for display.
        /// </summary>
        Task<TrialBalanceExtractionResultDto?> ExtractTrialBalanceAsync(byte[] fileBytes, string mimeType, TrialBalanceExtractionResultDto? currentSnapshot, AccountingStandard accountingStandard);

        /// <summary>
        /// Generates a comparable M&A/trading multiples table for the given company profile.
        /// Gemini returns only Category/RangeLow/RangeHigh/Median/Vintage/SourceBasis/IsEstimate per
        /// row — ImpliedEquityValueM is NOT requested here, the caller computes it from the
        /// project's own EBITDA.
        /// </summary>
        Task<List<ComparableMultiplesRowDto>?> GetComparableMultiplesAsync(ComparableMultiplesRequestDto request);
    }
}
