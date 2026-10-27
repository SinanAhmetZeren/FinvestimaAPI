namespace FinvestimaAPI.Models.ValuatorPro
{
    // L4 "Amortisman" panelindeki elle girilen tutar — task+yıl başına tek değer. Liste boş
    // gönderilirse (satır silinirse) motor otomatik hesaplamaya (alt hesap/birikmiş fark/%4
    // tahmin) geri döner.
    public class DepreciationOverride
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
    }
}
