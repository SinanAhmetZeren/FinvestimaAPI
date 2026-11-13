namespace FinvestimaAPI.Models
{
    // Kredi defteri: her satın alma/admin ekleme işlemi ve her task kullanımı AYRI bir satır —
    // tek bir sayaç değil. Bir email'in bakiyesi, o email'e ait tüm satırların Credits
    // toplamıdır (ekleme satırları pozitif, kullanım satırları negatif).
    public class CreditLedgerEntry
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string AddedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Bu satırın bakiyeye etkisi: ekleme satırında pozitif, task oluşturma (kullanım)
        // satırında -1.
        public int Credits { get; set; } = 1;
    }
}
