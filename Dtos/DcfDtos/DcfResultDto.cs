namespace FinvestimaAPI.Dtos.DcfDtos
{
    public class DcfResultDto
    {
        public decimal TotalPresentValue { get; set; }
        public List<DcfPeriodResultDto> Periods { get; set; } = new();
    }
}
