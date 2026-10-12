using System.Text;
using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers
{
    // Gemini'ye göndermeden önce, Excel'den çevrilmiş mizan metninden, kodu belirtilen rakam
    // sayısından (maxDigits) daha derin olan satırları çıkarır. Kullanıcı UI'da bu eşiği seçer
    // (3/5/7 hane ya da sınırsız) — amaç, 3. taraflara (banka şubesi/müşteri adı gibi) gidebilecek
    // bilgiyi, kullanıcının onayladığı derinlikte anonimleştirmek.
    public static class MizanDepthFilter
    {
        // Kod kolonunu bulmak için geniş desen: 1-3 haneli ana kod, ardından boşlukla ayrılmış
        // (harf içerebilen) alt gruplar — ör. "120 01 01 A005".
        private static readonly Regex CodeLikeCell = new(@"^\d{1,3}( [A-Za-z0-9]{1,8})*$");
        private const double MinMatchRatio = 0.5;

        public static string RemoveDeepestSubAccounts(string extractedText, int maxDigits)
        {
            if (maxDigits <= 0) maxDigits = int.MaxValue; // "tamamını kullan" — filtreleme yok

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

                    AppendFilteredSheet(sb, lines, blockStart, blockEnd, maxDigits);
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

        private static void AppendFilteredSheet(StringBuilder sb, string[] lines, int start, int end, int maxDigits)
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
                    if (digitCount > 0 && digitCount > maxDigits)
                        continue; // seçilen derinlikten fazla — atla
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
                bool hasHierarchy = false;
                foreach (var row in rows)
                {
                    if (c >= row.Length) continue;
                    var v = row[c].Trim();
                    if (v.Length == 0) continue;
                    total++;
                    if (CodeLikeCell.IsMatch(v))
                    {
                        matches++;
                        if (v.Contains(' ')) hasHierarchy = true;
                    }
                }
                if (total == 0) continue;
                var ratio = (double)matches / total;
                if (ratio < MinMatchRatio) continue;

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
