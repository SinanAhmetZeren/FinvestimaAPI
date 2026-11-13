namespace FinvestimaAPI.Services.Payment
{
    // appsettings/.env'den okunur: GarantiPos__TerminalId vb. Test işyeri hesabı Garanti'den
    // alınana kadar bunlar boş kalır — doldurulmadan ödeme akışı çalışmaz (bkz. GarantiPosService).
    public class GarantiPosOptions
    {
        public string TerminalId { get; set; } = string.Empty;
        public string TerminalProvUserId { get; set; } = string.Empty;
        public string TerminalUserId { get; set; } = string.Empty;
        public string TerminalMerchantId { get; set; } = string.Empty;
        public string StoreKey { get; set; } = string.Empty;
        public string ProvisioningPassword { get; set; } = string.Empty;
        public string CompanyName { get; set; } = "Finvestima";
        // TEST -> sanalposprovtest.garanti.com.tr, PROD -> sanalposprov.garanti.com.tr
        public string Mode { get; set; } = "TEST";
        public string SuccessUrl { get; set; } = string.Empty;
        public string ErrorUrl { get; set; } = string.Empty;
    }
}
