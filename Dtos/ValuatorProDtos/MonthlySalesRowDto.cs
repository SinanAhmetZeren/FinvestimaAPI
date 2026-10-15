namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    public class MonthlySalesRowDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Currency { get; set; } = "TRY";
        public decimal Revenue { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal Opex { get; set; }
    }
}
