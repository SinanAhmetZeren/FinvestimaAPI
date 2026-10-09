namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    // Gemini'den dönen response'un şekli: yalnızca YENİ dosyanın kapsadığı
    // yıl(lar) ve hesap satırları. Snapshot olarak gönderilen önceki yıllar
    // burada tekrar üretilmez.
    public class MizanExtractionResultDto
    {
        public List<MizanYilDto> MizanYillar { get; set; } = new();
    }
}
