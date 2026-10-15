namespace FinvestimaAPI.Models.ValuatorPro
{
    public class MonthlySalesRow
    {
        public Guid Id { get; set; }
        public Guid MonthlySalesObjectId { get; set; }
        public MonthlySalesObject? MonthlySalesObject { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string Currency { get; set; } = "TRY";
        public decimal Revenue { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal Opex { get; set; }
        public bool IsManualOverride { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
