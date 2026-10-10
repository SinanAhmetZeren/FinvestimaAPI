namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    // Gemini'den dönen response'un şekli: yalnızca YENİ dosyanın kapsadığı yıl(lar) ve hesap
    // satırları. Context olarak gönderilen snapshot'taki yıllar burada tekrar üretilmez.
    public class TrialBalanceExtractionResultDto
    {
        public List<TrialBalanceYearDto> TrialBalanceYears { get; set; } = new();
    }
}
