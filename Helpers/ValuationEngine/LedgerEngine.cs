using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers.ValuationEngine
{
    public class AccRow
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Dt { get; set; }
        public decimal Ct { get; set; }
        public decimal Db { get; set; }
        public decimal Cb { get; set; }
    }

    // Katkı seviyeleri/amortisman alt kırılımı için kullanılan ham (3 haneden derin) satır.
    public class SubRow
    {
        public string Code { get; set; } = string.Empty; // normalize edilmiş, boşluklu (örn. "770 01 14")
        public string D { get; set; } = string.Empty;     // sadece rakamlar (örn. "7700114")
        public string Name { get; set; } = string.Empty;
        public decimal Dt { get; set; }
        public decimal Ct { get; set; }
        public decimal Db { get; set; }
        public decimal Cb { get; set; }
    }

    public class Ledger
    {
        public int Year { get; set; }
        public int Months { get; set; } = 12;
        public bool Closed { get; set; }
        public Dictionary<string, AccRow> Acc { get; set; } = new();
        public List<SubRow> Sub { get; set; } = new();
    }

    public class RawAccountRow
    {
        public string Code { get; set; } = string.Empty;
        public string? Name { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal DebitBalance { get; set; }
        public decimal CreditBalance { get; set; }
    }

    // finvestimaweb/src/valuator-pro/engine/ledger.js'in C# portu — sadece defaultAssumptions
    // hesaplaması için gereken fonksiyonlar (buildLedger, sumBal, bal, flow, sumFlow,
    // depreciationFromSub). accName burada kullanılmıyor (isim zaten mizan'dan geliyor).
    public static class LedgerEngine
    {
        public static string NormCode(string? c) => Regex.Replace((c ?? string.Empty).Trim(), @"[\s.\-_/]+", " ").Trim();
        public static string Digits(string? c) => Regex.Replace(c ?? string.Empty, @"\D", "");

        public static string Fold(string? s)
        {
            var upper = (s ?? string.Empty).ToUpperInvariant();
            upper = upper.Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G').Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
            upper = Regex.Replace(upper, "[^A-Z0-9 ]", " ");
            return Regex.Replace(upper, @"\s+", " ").Trim();
        }

        public static Ledger BuildLedger(IEnumerable<RawAccountRow> rows, int year, int months)
        {
            var clean = rows
                .Select(r => new { Row = r, Code = NormCode(r.Code), D = Digits(r.Code) })
                .Where(r => r.D.Length >= 3 && Regex.IsMatch(r.D, "^[1-9]"))
                .ToList();

            var acc = new Dictionary<string, AccRow>();
            var sub = new List<SubRow>();
            var has3 = clean.Any(r => r.D.Length == 3);

            if (has3)
            {
                foreach (var r in clean)
                {
                    if (r.D.Length == 3) Merge(acc, r.D, r.Code, r.Row);
                    else if (Regex.IsMatch(r.D, "^[67]")) sub.Add(ToSub(r.Code, r.D, r.Row));
                }
            }
            else
            {
                var ds = clean.Select(r => r.D).ToList();
                foreach (var r in clean)
                {
                    var isLeaf = !ds.Any(o => o != r.D && o.StartsWith(r.D));
                    if (isLeaf) Merge(acc, r.D.Substring(0, 3), r.Code, r.Row);
                    if (Regex.IsMatch(r.D, "^[67]")) sub.Add(ToSub(r.Code, r.D, r.Row));
                }
            }

            decimal dt = 0, ct = 0;
            foreach (var a in acc.Values) { dt += a.Dt; ct += a.Ct; }

            decimal isMove = 0, isBal = 0;
            foreach (var kv in acc)
                if (kv.Key.StartsWith("6") && kv.Key != "690" && kv.Key != "692")
                {
                    isMove += kv.Value.Dt + kv.Value.Ct;
                    isBal += Math.Abs(kv.Value.Db - kv.Value.Cb);
                }
            var closed = isMove > 0 && isBal < isMove * 0.001m;

            return new Ledger { Year = year, Months = months, Acc = acc, Sub = sub, Closed = closed };
        }

        private static SubRow ToSub(string code, string d, RawAccountRow r) => new SubRow
        {
            Code = code, D = d, Name = r.Name ?? string.Empty, Dt = r.Debit, Ct = r.Credit, Db = r.DebitBalance, Cb = r.CreditBalance,
        };

        private static void Merge(Dictionary<string, AccRow> acc, string code3, string fullCode, RawAccountRow r)
        {
            if (!acc.TryGetValue(code3, out var a))
            {
                acc[code3] = new AccRow { Code = code3, Name = r.Name ?? string.Empty, Dt = r.Debit, Ct = r.Credit, Db = r.DebitBalance, Cb = r.CreditBalance };
            }
            else
            {
                a.Dt += r.Debit; a.Ct += r.Credit; a.Db += r.DebitBalance; a.Cb += r.CreditBalance;
                if (string.IsNullOrEmpty(a.Name)) a.Name = r.Name ?? string.Empty;
            }
        }

        // Bakiye hep Borç-Alacak (dt-ct) farkından — ayrı db/cb alanından değil (kaynakta tutarsız olabilir).
        public static decimal Bal(Ledger L, int code) => L.Acc.TryGetValue(code.ToString(), out var a) ? a.Dt - a.Ct : 0;

        public static decimal SumBal(Ledger L, int from, int to, int[]? ex = null)
        {
            decimal s = 0;
            foreach (var kv in L.Acc)
            {
                if (!int.TryParse(kv.Key, out var c)) continue;
                if (c >= from && c <= to && (ex == null || !ex.Contains(c))) s += kv.Value.Dt - kv.Value.Ct;
            }
            return s;
        }

        // Akış (gelir tablosu) tutarı — doğal yönde pozitif
        public static decimal Flow(Ledger L, int code)
        {
            if (!L.Acc.TryGetValue(code.ToString(), out var a)) return 0;
            var n = TekduzenAccounts.Nature(code);
            var net = a.Db - a.Cb;
            if (Math.Abs(net) > 0.5m) return n == 'C' ? -net : net;
            return n == 'C' ? a.Ct : a.Dt;
        }

        public static decimal SumFlow(Ledger L, IEnumerable<int> codes) => codes.Sum(c => Flow(L, c));

        public class DepreciationResult
        {
            public decimal Total { get; set; }
            public decimal Cogs { get; set; }
            public decimal Psd { get; set; }
            public decimal Ga { get; set; }
            public decimal Rnd { get; set; }
            public decimal Other { get; set; }
        }

        private static decimal Val(SubRow r)
        {
            var net = r.Db - r.Cb;
            return Math.Abs(net) > 0.5m ? net : r.Dt;
        }

        // Alt hesaplardan amortisman (730/760/770 … "AMORTİSMAN" isimli en üst seviye)
        public static DepreciationResult? DepreciationFromSub(Ledger L)
        {
            var cand = L.Sub
                .Where(r => Regex.IsMatch(Fold(r.Name), "AMORT") && !Regex.IsMatch(Fold(r.Name), "YANSITMA") && Regex.IsMatch(r.D, "^7[0-8]0|^6[2-3]"))
                .OrderBy(r => r.D.Length)
                .ToList();
            var kept = new List<SubRow>();
            foreach (var r in cand)
                if (!kept.Any(k => r.D.StartsWith(k.D))) kept.Add(r);
            if (kept.Count == 0) return null;

            decimal cogs = 0, psd = 0, ga = 0, rnd = 0, other = 0;
            foreach (var r in kept)
            {
                var p = r.D.Length >= 2 ? r.D.Substring(0, 2) : r.D;
                var v = Val(r);
                if (p == "73" || p == "72" || p == "71" || p == "74" || p == "62") cogs += v;
                else if (p == "76") psd += v;
                else if (p == "77") ga += v;
                else if (p == "75") rnd += v;
                else other += v;
            }
            return new DepreciationResult { Total = cogs + psd + ga + rnd + other, Cogs = cogs, Psd = psd, Ga = ga, Rnd = rnd, Other = other };
        }
    }
}
