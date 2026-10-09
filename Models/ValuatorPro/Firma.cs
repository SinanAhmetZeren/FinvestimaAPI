namespace FinvestimaAPI.Models.ValuatorPro
{
    public class Firma
    {
        public Guid Id { get; set; }
        public string Ad { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<MizanYil> MizanYillar { get; set; } = new();
    }
}
