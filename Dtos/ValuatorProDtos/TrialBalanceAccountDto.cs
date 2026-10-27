namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    public class TrialBalanceAccountDto
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        // Sadece GAAP'ta dolu: şirketin kendi dosyasındaki orijinal kod/isim. Tekdüzen'de
        // Code/Name zaten orijinal olduğu için null kalır.
        public string? OriginalCode { get; set; }
        public string? OriginalName { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
        public bool IsManualOverride { get; set; }
        public string? ContributionClass { get; set; }
        public bool ContributionClassIsManual { get; set; }
    }
}
