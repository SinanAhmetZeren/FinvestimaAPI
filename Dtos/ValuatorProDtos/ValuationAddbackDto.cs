namespace FinvestimaAPI.Dtos.ValuatorProDtos
{
    public class ValuationAddbackDto
    {
        public Guid? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Year { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "TRY";
        public bool HasEvidence { get; set; }
        public bool IsActive { get; set; }
    }
}
