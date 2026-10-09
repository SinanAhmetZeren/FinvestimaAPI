using System.Text;
using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers
{
    // Gemini'ye göndermeden önce, Excel'den çevrilmiş mizan metninden en derin hesap kırılımı
    // satırlarını çıkarır (bunlar genelde banka şube adı / müşteri-tedarikçi adı gibi üçüncü
    // taraf bilgisi taşır ve değerleme için gereksizdir — sadece 3'lü ana kod seviyesi gerekir).
    // Sadece satır ÇIKARIR, hiçbir toplama/kümüleme yapmaz.
    public static class MizanDepthFilter
    {
        // Kod kolonu, veriye bakılarak bulunur: hücrelerin çoğu BAŞTAN SONA sadece rakam
        // gruplarından ve aralarında tek bir ayraçtan (boşluk/nokta/tire) oluşuyorsa o kolon
        // kod kolonudur. Virgül ya da başka karakter varsa (ör. "90231.19" tutar hücresi,
        // "90.231,19" gibi virgüllü biçimler) eşleşmez — başlık metnine ya da ayraca
        // bağımlı değil, ama tutar/isim hücreleriyle karışmaz.
        private static readonly Regex CodeLikeCell = new(@"^\d{1,3}([ .\-]\d{1,3})*$");
        private const double MinMatchRatio = 0.5;

        // Kodun toplam rakam sayısı 3 (ana kod) + 2 + 2 = 7'den fazlaysa (4. gruba geçildiyse)
        // satır silinir — ayraç ne olursa olsun (boşluk/nokta/hiçbiri) sadece rakamlar sayılır.
        private const int MaxDigits = 7;

        public static string RemoveDeepestSubAccounts(string extractedText)
        {
            var lines = extractedText.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();

            int i = 0;
            while (i < lines.Length)
            {
                if (lines[i].StartsWith("Sheet: ", StringComparison.Ordinal))
                {
                    int blockStart = i;
                    int blockEnd = i + 1;
                    while (blockEnd < lines.Length && !lines[blockEnd].StartsWith("Sheet: ", StringComparison.Ordinal))
                        blockEnd++;

                    AppendFilteredSheet(sb, lines, blockStart, blockEnd);
                    i = blockEnd;
                }
                else
                {
                    sb.AppendLine(lines[i]);
                    i++;
                }
            }

            return sb.ToString();
        }

        private static void AppendFilteredSheet(StringBuilder sb, string[] lines, int start, int end)
        {
            var rows = new List<string[]>();
            for (int r = start; r < end; r++) rows.Add(lines[r].Split(','));

            int codeCol = FindCodeColumn(rows);
            if (codeCol < 0)
            {
                // Kod kolonu bulunamadı (mizan sayfası değil) — olduğu gibi bırak.
                for (int r = start; r < end; r++) sb.AppendLine(lines[r]);
                return;
            }

            for (int r = 0; r < rows.Count; r++)
            {
                var cells = rows[r];
                if (codeCol < cells.Length)
                {
                    var code = cells[codeCol].Trim();
                    var digitCount = code.Count(char.IsDigit);
                    if (digitCount > 0 && digitCount > MaxDigits)
                        continue; // en derin kırılım satırı — atla
                }
                sb.AppendLine(lines[start + r]);
            }
        }

        private static int FindCodeColumn(List<string[]> rows)
        {
            int maxCols = rows.Count > 0 ? rows.Max(r => r.Length) : 0;
            int bestCol = -1;
            double bestRatio = 0;
            bool bestHasHierarchy = false;

            for (int c = 0; c < maxCols; c++)
            {
                int total = 0, matches = 0;
                bool hasHierarchy = false; // en az bir hücre birden fazla grup içeriyor mu
                                            // (ör. "100 01") — "KOD UZUNLUK" gibi düz sayı
                                            // kolonlarında bu asla olmaz, gerçek kod kolonunu
                                            // ayırt etmemizi sağlar.
                foreach (var row in rows)
                {
                    if (c >= row.Length) continue;
                    var v = row[c].Trim();
                    if (v.Length == 0) continue;
                    total++;
                    if (CodeLikeCell.IsMatch(v))
                    {
                        matches++;
                        if (v.Any(ch => ch is ' ' or '.' or '-')) hasHierarchy = true;
                    }
                }
                if (total == 0) continue;
                var ratio = (double)matches / total;
                if (ratio < MinMatchRatio) continue;

                // Hiyerarşi gösteren bir kolon varsa, onu göstermeyen hiçbir kolona tercih et.
                var better = bestCol < 0
                    || (hasHierarchy && !bestHasHierarchy)
                    || (hasHierarchy == bestHasHierarchy && ratio > bestRatio);
                if (better)
                {
                    bestRatio = ratio;
                    bestCol = c;
                    bestHasHierarchy = hasHierarchy;
                }
            }

            return bestCol;
        }
    }
}
