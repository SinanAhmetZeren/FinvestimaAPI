namespace FinvestimaAPI.Helpers.ValuationEngine
{
    public class FxRate { public decimal Avg { get; set; } public decimal End { get; set; } }

    public class Cost7
    {
        public decimal Mat, Lab, Ovh, Rnd, Psd, Ga, Fin;
    }

    public class DaBreakdown
    {
        public decimal Total, Cogs, Psd, Ga, Rnd, Opex;
        public string Source { get; set; } = "pct";
    }

    public class IsResult
    {
        public decimal GrossSales, Deductions, NetSales, Domestic, ExportS, Cogs, Gross, Rnd, Psd, Ga, Opex, Ebit,
            OtherInc, OtherExp, FxGain, FxLoss, Infl, FinExp, Ordinary, ExtraInc, ExtraExp, Ebt, Tax, Net, Ebitda;
        public Cost7 Cost7 { get; set; } = new();
        public DaBreakdown Da { get; set; } = new();
    }

    public class BsResult
    {
        public Dictionary<string, decimal> V { get; set; } = new();
        public decimal CA, NCA, TA, STL, LTL, EQ, TP, Check, FinDebt, Cash, NetDebt, Partners, Nwc, OpCA, OpCL,
            NetAssets, InvestedCapital, TangibleEquity, ComputedProfit;
    }

    public class UsdYear
    {
        public decimal Rev, Cogs, Gross, Psd, Ga, Rnd, Opex, Ebit, Da, DaCogs, DaOpex, DaPsd, DaGa, DaRnd, Ebitda,
            EbitdaAdj, Addb, FinExp, Ebt, Tax, Net, Mat, Lab, Ovh, Nwc, NetDebt, Equity, Ic, Cash, FinDebt, Ta,
            Inv, TradeRec, TradePay, Intang, Ppe;
    }

    public class HistYear
    {
        public int Year { get; set; }
        public Ledger L { get; set; } = new();
        public BsResult Bs { get; set; } = new();
        public IsResult Is { get; set; } = new();
        public decimal Addb, FxInc, EbitdaAdj;
        public FxRate Fx { get; set; } = new();
        public UsdYear Usd { get; set; } = new();
    }

    public class AddbackInput { public int Year; public decimal Tl; public string Currency = "TRY"; public bool On; }
    public class DepreciationOverrideInput { public int Year; public decimal Amount; }

    public class HistoryState
    {
        public bool PartnersAsDebt = true;
        public bool FxInEbitda = false;
        public Dictionary<int, FxRate>? FxOverrides;
        public List<AddbackInput> Addbacks = new();
        public List<DepreciationOverrideInput> DaOverrides = new();
    }

    // finvestimaweb/src/valuator-pro/engine/statements.js'in C# portu — sadece defaultAssumptions
    // (baseYear/histCagr) için gereken alanlar hesaplanıyor.
    public static class StatementsEngine
    {
        // Kaynak Excel'deki "Aylık USD_TRY Kurlar" sayfası — frontend'deki DEFAULT_FX ile birebir.
        public static readonly Dictionary<int, FxRate> DefaultFx = new()
        {
            [2020] = new() { Avg = 7.02m, End = 7.43m }, [2021] = new() { Avg = 8.89m, End = 13.32m },
            [2022] = new() { Avg = 16.57m, End = 18.70m }, [2023] = new() { Avg = 23.78m, End = 29.4913m },
            [2024] = new() { Avg = 32.855m, End = 35.2868m }, [2025] = new() { Avg = 39.537m, End = 42.9395m },
            [2026] = new() { Avg = 44.616m, End = 47.075m },
        };

        public static BsResult BuildBs(Ledger L, bool partnersAsDebt)
        {
            var v = new Dictionary<string, decimal>();
            decimal ca = 0, nca = 0, stl = 0, ltl = 0, eq = 0;

            void Sum(List<BsSection> side, bool isAsset)
            {
                foreach (var s in side)
                {
                    decimal total = 0;
                    foreach (var l in s.Lines)
                    {
                        var raw = LedgerEngine.SumBal(L, l.From, l.To, l.Ex);
                        var val = isAsset ? raw : -raw;
                        v[l.K] = val;
                        total += val;
                    }
                    if (s.Sec == "Dönen Varlıklar") ca = total;
                    else if (s.Sec == "Duran Varlıklar") nca = total;
                    else if (s.Sec == "Kısa Vadeli Yabancı Kaynaklar") stl = total;
                    else if (s.Sec == "Uzun Vadeli Yabancı Kaynaklar") ltl = total;
                    else if (s.Sec == "Özkaynaklar") eq = total;
                }
            }
            Sum(TekduzenAccounts.BsAssets, true);
            Sum(TekduzenAccounts.BsLiabs, false);

            // Kapanmamış mizan: dönem kârı özkaynakta yoksa gelir tablosu bakiyelerinden hesapla
            decimal computedProfit = 0;
            var has590 = Math.Abs(LedgerEngine.Bal(L, 590)) + Math.Abs(LedgerEngine.Bal(L, 591)) > 0.5m;
            if (!L.Closed && !has590)
            {
                computedProfit = -(LedgerEngine.SumBal(L, 600, 689) + LedgerEngine.Bal(L, 691));
                if (Math.Abs(computedProfit) > 0.5m)
                {
                    v["netProfit"] = v.GetValueOrDefault("netProfit") + computedProfit;
                    eq += computedProfit;
                }
            }

            var ta = ca + nca;
            var tp = stl + ltl + eq;
            var partners = v.GetValueOrDefault("partnersST") + v.GetValueOrDefault("partnersLT");
            var finDebt = v.GetValueOrDefault("finDebtST") + v.GetValueOrDefault("finDebtLT") + (partnersAsDebt ? partners : 0);
            var cash = v.GetValueOrDefault("cash") + v.GetValueOrDefault("secs");
            var netDebt = finDebt - cash;
            var opCA = v.GetValueOrDefault("tradeRec") + v.GetValueOrDefault("otherRec") + v.GetValueOrDefault("inv")
                + v.GetValueOrDefault("advGiven") + v.GetValueOrDefault("constr") + v.GetValueOrDefault("prepaid") + v.GetValueOrDefault("otherCA");
            var opCL = v.GetValueOrDefault("tradePay") + v.GetValueOrDefault("otherPayST") + v.GetValueOrDefault("advRecv")
                + v.GetValueOrDefault("constrPr") + v.GetValueOrDefault("taxPay") + v.GetValueOrDefault("provST")
                + v.GetValueOrDefault("defIncST") + v.GetValueOrDefault("otherSTL");
            var nwc = opCA - opCL;
            var investedCapital = eq + v.GetValueOrDefault("finDebtST") + v.GetValueOrDefault("finDebtLT") + partners - cash;

            return new BsResult
            {
                V = v, CA = ca, NCA = nca, TA = ta, STL = stl, LTL = ltl, EQ = eq, TP = tp, Check = ta - tp,
                FinDebt = finDebt, Cash = cash, NetDebt = netDebt, Partners = partners, Nwc = nwc, OpCA = opCA, OpCL = opCL,
                NetAssets = eq, InvestedCapital = investedCapital, TangibleEquity = eq - v.GetValueOrDefault("intang"),
                ComputedProfit = computedProfit,
            };
        }

        private static DaBreakdown Split(decimal t, string source) => new() { Total = t, Cogs = t * 0.8m, Psd = t * 0.1m, Ga = t * 0.1m, Rnd = 0, Source = source };

        public static IsResult BuildIs(Ledger L, Ledger? prevL, decimal annualize, decimal? daOverride)
        {
            var k = annualize;
            decimal F(params int[] codes) => LedgerEngine.SumFlow(L, codes) * k;
            decimal FRange(int a, int b) => LedgerEngine.SumFlow(L, Enumerable.Range(a, b - a + 1)) * k;

            var grossSales = F(600, 601, 602);
            var deductions = F(610, 611, 612);
            var netSales = grossSales - deductions;
            var cogs = F(620, 621, 622, 623);
            var gross = netSales - cogs;
            var rnd = F(630); var psd = F(631); var ga = F(632);
            var opex = rnd + psd + ga;
            var ebit = gross - opex;
            var otherInc = FRange(640, 649); var otherExp = FRange(650, 659);
            var fxGain = F(646); var fxLoss = F(656); var infl = F(648) - F(658);
            var finExp = F(660, 661);
            var ordinary = ebit + otherInc - otherExp - finExp;
            var extraInc = F(671, 679); var extraExp = F(680, 681, 689);
            var ebt = ordinary + extraInc - extraExp;
            var tax = F(691);
            var net = ebt - tax;
            var cost7 = new Cost7 { Mat = F(710), Lab = F(720), Ovh = F(730), Rnd = F(750), Psd = F(760), Ga = F(770), Fin = F(780) };
            var domestic = F(600); var exportS = F(601);

            DaBreakdown? da = null;
            var sub = LedgerEngine.DepreciationFromSub(L);
            if (sub != null && sub.Total > 0)
                da = new DaBreakdown { Total = sub.Total * k, Cogs = sub.Cogs * k, Psd = sub.Psd * k, Ga = (sub.Ga + sub.Other) * k, Rnd = sub.Rnd * k, Source = "sub" };
            else if (prevL != null)
            {
                decimal Acc(Ledger l) => -(LedgerEngine.Bal(l, 257) + LedgerEngine.Bal(l, 268) + LedgerEngine.Bal(l, 278) + LedgerEngine.Bal(l, 299));
                var d = Acc(L) - Acc(prevL);
                if (d > 0) da = Split(d * k, "delta");
            }
            da ??= Split(netSales * 0.04m, "pct");
            if (daOverride != null)
            {
                var t = daOverride.Value;
                var f = da.Total != 0 ? t / da.Total : 0;
                da = da.Total != 0
                    ? new DaBreakdown { Total = t, Cogs = da.Cogs * f, Psd = da.Psd * f, Ga = da.Ga * f, Rnd = da.Rnd * f, Source = "manual" }
                    : Split(t, "manual");
            }
            da.Opex = da.Psd + da.Ga + da.Rnd;
            var ebitda = ebit + da.Total;

            return new IsResult
            {
                GrossSales = grossSales, Deductions = deductions, NetSales = netSales, Domestic = domestic, ExportS = exportS,
                Cogs = cogs, Gross = gross, Rnd = rnd, Psd = psd, Ga = ga, Opex = opex, Ebit = ebit, OtherInc = otherInc,
                OtherExp = otherExp, FxGain = fxGain, FxLoss = fxLoss, Infl = infl, FinExp = finExp, Ordinary = ordinary,
                ExtraInc = extraInc, ExtraExp = extraExp, Ebt = ebt, Tax = tax, Net = net, Cost7 = cost7, Da = da, Ebitda = ebitda,
            };
        }

        public static List<HistYear> BuildHistory(List<Ledger> ledgers, HistoryState state)
        {
            var sorted = ledgers.OrderBy(l => l.Year).ToList();
            var years = new List<HistYear>();

            for (int i = 0; i < sorted.Count; i++)
            {
                var L = sorted[i];
                Ledger? prev = i > 0 && sorted[i - 1].Year == L.Year - 1 ? sorted[i - 1] : null;
                decimal annualize = L.Months > 0 && L.Months < 12 ? 12m / L.Months : 1;
                var bs = BuildBs(L, state.PartnersAsDebt);
                var daOv = state.DaOverrides.FirstOrDefault(d => d.Year == L.Year);
                var is_ = BuildIs(L, prev, annualize, daOv != null ? daOv.Amount : (decimal?)null);
                var fx = (state.FxOverrides != null && state.FxOverrides.TryGetValue(L.Year, out var ov)) ? ov
                    : DefaultFx.TryGetValue(L.Year, out var d) ? d : new FxRate { Avg = 1, End = 1 };

                var addb = state.Addbacks.Where(a => a.On && a.Year == L.Year)
                    .Sum(a => a.Tl * (a.Currency == "USD" ? (fx.Avg != 0 ? fx.Avg : 1) : 1));
                var fxInc = state.FxInEbitda ? is_.FxGain - is_.FxLoss : 0;
                var ebitdaAdj = is_.Ebitda + addb + fxInc;

                years.Add(new HistYear { Year = L.Year, L = L, Bs = bs, Is = is_, Addb = addb, FxInc = fxInc, EbitdaAdj = ebitdaAdj, Fx = fx });
            }

            foreach (var y in years)
            {
                var a = y.Fx.Avg != 0 ? y.Fx.Avg : 1;
                var e = y.Fx.End != 0 ? y.Fx.End : a;
                decimal Fi(decimal x) => x / a / 1000;
                decimal Fb(decimal x) => x / e / 1000;
                var I = y.Is; var B = y.Bs;
                y.Usd = new UsdYear
                {
                    Rev = Fi(I.NetSales), Cogs = Fi(I.Cogs), Gross = Fi(I.Gross), Psd = Fi(I.Psd), Ga = Fi(I.Ga), Rnd = Fi(I.Rnd), Opex = Fi(I.Opex),
                    Ebit = Fi(I.Ebit), Da = Fi(I.Da.Total), DaCogs = Fi(I.Da.Cogs), DaOpex = Fi(I.Da.Opex), DaPsd = Fi(I.Da.Psd), DaGa = Fi(I.Da.Ga), DaRnd = Fi(I.Da.Rnd),
                    Ebitda = Fi(I.Ebitda), EbitdaAdj = Fi(y.EbitdaAdj), Addb = Fi(y.Addb), FinExp = Fi(I.FinExp), Ebt = Fi(I.Ebt), Tax = Fi(I.Tax), Net = Fi(I.Net),
                    Mat = Fi(I.Cost7.Mat), Lab = Fi(I.Cost7.Lab), Ovh = Fi(I.Cost7.Ovh),
                    Nwc = Fb(B.Nwc), NetDebt = Fb(B.NetDebt), Equity = Fb(B.EQ), Ic = Fb(B.InvestedCapital), Cash = Fb(B.Cash), FinDebt = Fb(B.FinDebt),
                    Ta = Fb(B.TA), Inv = Fb(B.V.GetValueOrDefault("inv")), TradeRec = Fb(B.V.GetValueOrDefault("tradeRec")),
                    TradePay = Fb(B.V.GetValueOrDefault("tradePay")), Intang = Fb(B.V.GetValueOrDefault("intang")), Ppe = Fb(B.V.GetValueOrDefault("ppe")),
                };
            }
            return years;
        }

        // 7/A maliyet karması: SMM (amortisman hariç) bileşenlere bölünür
        public static (decimal mat, decimal lab, decimal ovh) CogsMix(HistYear y)
        {
            var c = y.Is.Cost7; var daC = y.Is.Da.Cogs;
            var ovhEx = Math.Max(0, c.Ovh - daC);
            var t = c.Mat + c.Lab + ovhEx;
            if (t <= 0) return (0.7m, 0.1m, 0.2m);
            return (c.Mat / t, c.Lab / t, ovhEx / t);
        }
    }
}
