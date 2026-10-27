using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers
{
    // finvestimaweb/src/valuator-pro/engine/contribution.js'teki classify()'ın C# birebir karşılığı
    // — mizan yüklendiğinde 7'li gider alt hesaplarına varsayılan katkı seviyesi (B-F) atamak için
    // burada da çalıştırılıyor, frontend sadece elle override edilen değeri üstüne yazabiliyor.
    public static class ContributionClassifier
    {
        public static string Fold(string s)
        {
            var upper = (s ?? string.Empty).ToUpperInvariant();
            upper = upper.Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
                .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
            upper = Regex.Replace(upper, "[^A-Z0-9 ]", " ");
            upper = Regex.Replace(upper, "\\s+", " ").Trim();
            return upper;
        }

        // code: ham mizan kodu (ör. "770 01 14"). İlk 3 haneli kök "parent" olarak kullanılır.
        public static string Classify(string code, string name)
        {
            var n = Fold(name);
            var digits = Regex.Replace(code ?? string.Empty, "\\D", "");
            var parent = digits.Length >= 3 ? digits.Substring(0, 3) : digits;

            if (Regex.IsMatch(n, "AMORT")) return "F";
            if (parent == "710") return "B";
            if (parent == "720") return "C";
            if (parent is "760" or "770" or "780" or "750") return "F";
            if (Regex.IsMatch(n, "FASON|SARF|KLISE|NAVLUN|ITHALAT|ENERJI|ELEKTRIK|DOGALGAZ|AMBALAJ")) return "B";
            if (Regex.IsMatch(n, "DIREK|MAK.*BAKIM|TEMIZLIK|GUVENLIK")) return "C";
            if (Regex.IsMatch(n, "YEMEK|TASIMA|SERVIS|SU GIDER|NAKLIYE")) return "D";
            return "E";
        }
    }
}
