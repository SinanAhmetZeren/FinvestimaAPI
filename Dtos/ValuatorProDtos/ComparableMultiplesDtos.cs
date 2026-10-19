namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    public class ComparableMultiplesRequestDto
    {
        // Son başarılı araştırmayı sayfa yenilemesinde göstermek için job buna bağlanır.
        public Guid ValuationTaskId { get; set; }
        public string SectorSubSector { get; set; } = string.Empty;
        public decimal YearlySales { get; set; }
        public string Region { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        // Normalized FAVÖK/EBITDA basis, THOUSAND USD (V.m2.basis'in birimiyle aynı) — read from
        // the project itself on the frontend, never asked of the user.
        public decimal Ebitda { get; set; }
        // "tr" | "en" — hangi ağaçtan (Tekdüzen/GAAP) çağrıldığını belirtir, Gemini'nin yanıt
        // dilini belirler. Boşsa İngilizce varsayılır.
        public string Language { get; set; } = "en";
    }

    // Gemini'den dönen şekil — ImpliedEquityValueM burada YOK, Gemini'nin hesapladığı/bildiği bir
    // şey değil; controller Median*Ebitda ile kendisi hesaplayıp response'a ekliyor.
    public class ComparableMultiplesRowDto
    {
        public string Category { get; set; } = string.Empty;
        public decimal RangeLow { get; set; }
        public decimal RangeHigh { get; set; }
        public decimal Median { get; set; }
        public string Vintage { get; set; } = string.Empty;
        public string SourceBasis { get; set; } = string.Empty;
        public string? SourceUrl { get; set; }
        public bool IsEstimate { get; set; }
        public decimal ImpliedEquityValueM { get; set; }
    }

    public class ComparableMultiplesResultDto
    {
        public List<ComparableMultiplesRowDto> Rows { get; set; } = new();
    }

    // L6'daki kalıcı (DB'de saklanan) Emsal çarpanlar tablosu satırı — "Kullan" (AI popup'tan) ya
    // da elle girilip "Kaydet" ile yazılır. Id yoksa (null/boş) yeni satır olarak eklenir.
    public class ComparableMultiplesTableRowDto
    {
        public Guid? Id { get; set; }
        public string Category { get; set; } = string.Empty;
        public decimal RangeLow { get; set; }
        public decimal RangeHigh { get; set; }
        public decimal Median { get; set; }
        public string? Vintage { get; set; }
        public string? SourceBasis { get; set; }
        public string? SourceUrl { get; set; }
        public bool IsEstimate { get; set; }
    }
}
