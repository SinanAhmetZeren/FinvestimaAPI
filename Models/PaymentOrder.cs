namespace FinvestimaAPI.Models
{
    public enum PaymentOrderStatus { Pending, Paid, Failed }

    // Garanti Sanal POS'a gönderilen her ödeme denemesi için bir satır — callback'te bu
    // satırdaki OrderId/Email/Credits üzerinden hangi işlemin onaylandığı bulunur.
    public class PaymentOrder
    {
        public Guid Id { get; set; }
        public string OrderId { get; set; } = string.Empty; // bankaya giden benzersiz sipariş no
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal AmountTl { get; set; }
        public int Credits { get; set; }
        public PaymentOrderStatus Status { get; set; } = PaymentOrderStatus.Pending;
        public string? BankMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
