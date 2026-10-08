namespace FinvestimaAPI.Models
{
    public class ExchangeRate
    {
        public int Id { get; set; }
        public DateOnly Date { get; set; }

        public decimal UsdTryForexBuying { get; set; }
        public decimal UsdTryForexSelling { get; set; }

        public decimal EurTryForexBuying { get; set; }
        public decimal EurTryForexSelling { get; set; }

        public decimal GbpTryForexBuying { get; set; }
        public decimal GbpTryForexSelling { get; set; }

        public decimal EurUsdCrossRate { get; set; }
    }
}
