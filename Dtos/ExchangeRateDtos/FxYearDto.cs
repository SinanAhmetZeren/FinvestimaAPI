namespace FinvestimaAPI.Dtos.ExchangeRateDtos
{
    public class FxYearDto
    {
        public int Year { get; set; }
        public decimal? Avg { get; set; }
        public decimal? End { get; set; }
        public bool Partial { get; set; }
    }
}
