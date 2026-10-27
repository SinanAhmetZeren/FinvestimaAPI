namespace FinvestimaAPI.Helpers.ValuationEngine
{
    // finvestimaweb/src/valuator-pro/engine/valuation.js'teki baseYear/histCagr/defaultAssumptions'ın
    // C# portu. Çıktı, frontend'in `asm` objesiyle (alan adları dahil, camelCase serileştirilince)
    // birebir aynı olacak şekilde kuruldu — Statements.jsx/Valuation.jsx bunu doğrudan okuyabilsin diye.
    public static class ValuationDefaults
    {
        private static decimal Clamp(decimal x, decimal a, decimal b) => Math.Min(b, Math.Max(a, x));

        public class BaseYearResult
        {
            public int Year;
            public decimal Rev, Ebitda, Da, Nwc, NetDebt, Equity, Ic, Intang, FinExp, FinDebt;
            public Dictionary<string, decimal> Pct { get; set; } = new();
        }

        public static BaseYearResult? BaseYear(List<HistYear> hist)
        {
            if (hist.Count == 0) return null;
            var b = hist[^1];
            var u = b.Usd;
            var (mat, lab, ovh) = StatementsEngine.CogsMix(b);
            var cogsEx = Math.Max(0, u.Cogs - u.DaCogs);
            var rev = u.Rev != 0 ? u.Rev : 1;
            var gaEx = Math.Max(0, u.Ga - u.DaGa - u.Addb);
            return new BaseYearResult
            {
                Year = b.Year, Rev = u.Rev, Ebitda = u.EbitdaAdj, Da = u.Da, Nwc = u.Nwc, NetDebt = u.NetDebt,
                Equity = u.Equity, Ic = u.Ic, Intang = u.Intang, FinExp = u.FinExp, FinDebt = u.FinDebt,
                Pct = new Dictionary<string, decimal>
                {
                    ["mat"] = cogsEx * mat / rev, ["lab"] = cogsEx * lab / rev, ["ovh"] = cogsEx * ovh / rev,
                    ["psd"] = Math.Max(0, u.Psd - u.DaPsd) / rev, ["ga"] = gaEx / rev, ["rnd"] = Math.Max(0, u.Rnd - u.DaRnd) / rev,
                },
            };
        }

        public static double? HistCagr(List<HistYear> hist)
        {
            if (hist.Count < 2) return null;
            var a = hist[0].Usd.Rev; var b = hist[^1].Usd.Rev;
            var n = hist[^1].Year - hist[0].Year;
            if (a <= 0 || n <= 0) return null;
            return Math.Pow((double)(b / a), 1.0 / n) - 1;
        }

        // Dönüş değeri, frontend'deki `asm` objesiyle birebir aynı şekle sahip — SaveAssumptions
        // endpoint'ine doğrudan verilebilir (ContributionClass gibi ayrı bir DTO'ya gerek yok).
        public static object? DefaultAssumptions(List<HistYear> hist)
        {
            var B = BaseYear(hist);
            if (B == null) return null;
            var cg = HistCagr(hist);
            decimal g = cg == null ? 0.08m : Math.Round(Clamp((decimal)cg.Value, 0.03m, 0.15m) * 100) / 100;
            var costs = B.Pct.ToDictionary(kv => kv.Key, kv => new { s = kv.Value, e = kv.Value });
            var de = B.Equity > 0 ? Clamp(B.FinDebt / B.Equity, 0, 1.5m) : 0.5m;

            return new
            {
                N = 5,
                growth = Enumerable.Repeat((double)g, 7).ToArray(),
                y1Mode = "growth",
                runRateFactor = (double?)null,
                costs,
                capexPct = Clamp(B.Rev != 0 ? (B.Da / B.Rev) * 1.2m : 0.05m, 0.03m, 0.12m),
                nwcPct = Clamp(B.Rev != 0 ? B.Nwc / B.Rev : 0.2m, -0.2m, 0.8m),
                daLife = 10, daFade = 0, taxRate = 0.25, interestDecline = 0.2,
                wacc = new { manual = false, value = 0.125, rf = 0.043, erp = 0.055, crp = 0.030, betaU = 0.85, size = 0.020, kd = 0.090, de = Math.Round(de * 100) / 100 },
                tg = 0.03, tvMethod = "gordon", exitMult = 6.0, midYear = true,
                mult = new { sector = "trSme", value = 6.0, basis = "ltm", disc = 0 },
                na = new { uplift = 0, excludeIntang = false },
                weights = new { dcf = 0.4, mult = 0.4, na = 0.2 },
                scenario = "base",
                _base = B.Year,
            };
        }
    }
}
