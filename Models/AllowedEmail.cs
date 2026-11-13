namespace FinvestimaAPI.Models
{
    // Kredi defteri: her admin ekleme işlemi ve her task kullanımı AYRI bir satır — tek bir
    // sayaç değil. Bir email'in bakiyesi, o email'e ait tüm satırların TaskCredits toplamıdır
    // (ekleme satırları pozitif, kullanım satırları negatif, bkz. AccountController.Register).
    public class AllowedEmail
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string NormalizedEmail { get; set; } = string.Empty;
        public string? Note { get; set; }
        public string AddedByUserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Bu satırın bakiyeye etkisi: admin ekleme satırında pozitif, task oluşturma
        // (kullanım) satırında -1.
        public int TaskCredits { get; set; } = 1;
    }
}
