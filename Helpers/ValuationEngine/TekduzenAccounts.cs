namespace FinvestimaAPI.Helpers.ValuationEngine
{
    // finvestimaweb/src/valuator-pro/engine/accounts.js'in C# portu — sadece defaultAssumptions
    // hesaplaması için gereken kısımlar (ACCOUNTS adları gerekmiyor, sadece nature + BS satırları).
    public class BsLine
    {
        public string K { get; set; } = string.Empty;
        public int From { get; set; }
        public int To { get; set; }
        public int[] Ex { get; set; } = System.Array.Empty<int>();
    }

    public class BsSection
    {
        public string Sec { get; set; } = string.Empty;
        public List<BsLine> Lines { get; set; } = new();
    }

    public static class TekduzenAccounts
    {
        private static readonly HashSet<int> Contra = new(new[] {
            103,119,122,124,129,137,139,158,199,222,229,237,239,241,243,244,246,247,249,257,268,278,298,299,
            302,308,322,337,371,402,408,422,437,501,503,580,591,
        });

        // 'D' borç bakiyeli, 'C' alacak bakiyeli
        public static char Nature(int code)
        {
            int k = code / 100, t = code / 10;
            char n;
            if (k == 1 || k == 2) n = 'D';
            else if (k == 3 || k == 4 || k == 5) n = 'C';
            else if (k == 6) n = (t == 60 || t == 64 || t == 67) ? 'C' : 'D';
            else n = 'D';
            if (Contra.Contains(code)) n = n == 'D' ? 'C' : 'D';
            return n;
        }

        public static readonly List<BsSection> BsAssets = new()
        {
            new BsSection { Sec = "Dönen Varlıklar", Lines = new() {
                new() { K="cash", From=100, To=109 }, new() { K="secs", From=110, To=119 },
                new() { K="tradeRec", From=120, To=129 }, new() { K="otherRec", From=130, To=139 },
                new() { K="inv", From=150, To=158 }, new() { K="advGiven", From=159, To=159 },
                new() { K="constr", From=170, To=179 }, new() { K="prepaid", From=180, To=189 },
                new() { K="otherCA", From=190, To=199 },
            }},
            new BsSection { Sec = "Duran Varlıklar", Lines = new() {
                new() { K="ltRec", From=220, To=229 }, new() { K="ltOtherRec", From=230, To=239 },
                new() { K="finFA", From=240, To=249 }, new() { K="ppe", From=250, To=258 },
                new() { K="ppeAdv", From=259, To=259 }, new() { K="intang", From=260, To=269 },
                new() { K="depl", From=270, To=279 }, new() { K="ltPrepaid", From=280, To=289 },
                new() { K="otherNCA", From=290, To=299 },
            }},
        };

        public static readonly List<BsSection> BsLiabs = new()
        {
            new BsSection { Sec = "Kısa Vadeli Yabancı Kaynaklar", Lines = new() {
                new() { K="finDebtST", From=300, To=309 }, new() { K="tradePay", From=320, To=329 },
                new() { K="partnersST", From=331, To=331 }, new() { K="otherPayST", From=330, To=339, Ex=new[]{331} },
                new() { K="advRecv", From=340, To=349 }, new() { K="constrPr", From=350, To=359 },
                new() { K="taxPay", From=360, To=369 }, new() { K="provST", From=370, To=379 },
                new() { K="defIncST", From=380, To=389 }, new() { K="otherSTL", From=390, To=399 },
            }},
            new BsSection { Sec = "Uzun Vadeli Yabancı Kaynaklar", Lines = new() {
                new() { K="finDebtLT", From=400, To=409 }, new() { K="tradePayLT", From=420, To=429 },
                new() { K="partnersLT", From=431, To=431 }, new() { K="otherPayLT", From=430, To=439, Ex=new[]{431} },
                new() { K="advRecvLT", From=440, To=449 }, new() { K="provLT", From=470, To=479 },
                new() { K="defIncLT", From=480, To=489 }, new() { K="otherLTL", From=490, To=499 },
            }},
            new BsSection { Sec = "Özkaynaklar", Lines = new() {
                new() { K="paidIn", From=500, To=509 }, new() { K="capRes", From=520, To=529 },
                new() { K="profitRes", From=540, To=549 }, new() { K="prevProfit", From=570, To=579 },
                new() { K="prevLoss", From=580, To=589 }, new() { K="netProfit", From=590, To=599 },
            }},
        };
    }
}
