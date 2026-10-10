using System.Text;
using System.Text.RegularExpressions;

namespace FinvestimaAPI.Helpers
{
    // Gemini'ye göndermeden önce, Excel'den çevrilmiş mizan metninden, hesap adı Tekdüzen Hesap
    // Planı'nda (Resources/tekduzen-hesap-plani.txt) tanımlı bilinen bir isimle eşleşmeyen
    // satırları çıkarır (bunlar genelde banka şube adı / müşteri-tedarikçi adı gibi üçüncü taraf
    // bilgisi taşır). Derinlik önemli değil — 3 haneden fazla kodlu satırlar da (aggregation/
    // kümüleme karşılaştırması için faydalı oldukları için) kalır, sadece isim bilinmiyorsa silinir.
    public static class MizanDepthFilter
    {
        // Kod kolonunu bulmak için geniş desen: 1-3 haneli ana kod, ardından boşlukla ayrılmış
        // (harf içerebilen) alt gruplar — ör. "120 01 01 A005".
        private static readonly Regex CodeLikeCell = new(@"^\d{1,3}( [A-Za-z0-9]{1,8})*$");
        private const double MinMatchRatio = 0.5;

        // İsim kolonunu bulmak için: en az iki ardışık harf içeren hücrelerin oranı en yüksek
        // olan (kod kolonu hariç) kolon isim kolonudur.
        private static readonly Regex HasLetters = new(@"[A-Za-zÇĞİÖŞÜçğıöşü]{2,}");

        private static readonly Lazy<HashSet<string>> KnownNames = new(LoadKnownNames);

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

            int nameCol = FindNameColumn(rows, codeCol);

            for (int r = 0; r < rows.Count; r++)
            {
                var cells = rows[r];
                var hasCode = codeCol < cells.Length && cells[codeCol].Trim().Length > 0;
                if (hasCode && nameCol >= 0 && nameCol < cells.Length)
                {
                    var name = cells[nameCol].Trim();
                    if (name.Length > 0 && !IsKnownName(name))
                        continue; // bilinmeyen isim (üçüncü taraf olabilir) — atla
                }
                sb.AppendLine(lines[start + r]);
            }
        }

        private static bool IsKnownName(string name)
        {
            var folded = Fold(name);
            if (folded.Length == 0) return true; // boş isim, engelleme
            foreach (var known in KnownNames.Value)
                if (folded.Contains(known) || known.Contains(folded))
                    return true;
            return false;
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

        private static int FindNameColumn(List<string[]> rows, int codeCol)
        {
            int maxCols = rows.Count > 0 ? rows.Max(r => r.Length) : 0;
            int bestCol = -1;
            double bestRatio = 0;

            for (int c = 0; c < maxCols; c++)
            {
                if (c == codeCol) continue;
                int total = 0, matches = 0;
                foreach (var row in rows)
                {
                    if (c >= row.Length) continue;
                    var v = row[c].Trim();
                    if (v.Length == 0) continue;
                    total++;
                    if (HasLetters.IsMatch(v)) matches++;
                }
                if (total == 0) continue;
                var ratio = (double)matches / total;
                if (ratio > bestRatio)
                {
                    bestRatio = ratio;
                    bestCol = c;
                }
            }

            return bestCol;
        }

        private static HashSet<string> LoadKnownNames()
        {
            var set = new HashSet<string>();
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Resources", "tekduzen-hesap-plani.txt");
                foreach (var line in File.ReadAllLines(path))
                {
                    var m = Regex.Match(line, @"^\d+\.\s*(.+)$");
                    if (m.Success)
                    {
                        var folded = Fold(m.Groups[1].Value);
                        if (folded.Length > 0) set.Add(folded);
                    }
                }
            }
            catch { /* referans okunamazsa boş liste — hiçbir satır eşleşmez, filtre hiçbir şeyi engellemez değil, her şeyi eler; kabul edilebilir güvenli taraf */ }
            return set;
        }

        // Frontend'deki fold() ile aynı mantık: büyük harfe çevir, Türkçe karakterleri sadeleştir,
        // alfanümerik olmayanları boşlukla değiştir, fazla boşlukları temizle.
        private static string Fold(string s)
        {
            var upper = s.ToUpperInvariant()
                .Replace('İ', 'I').Replace('Ş', 'S').Replace('Ğ', 'G')
                .Replace('Ü', 'U').Replace('Ö', 'O').Replace('Ç', 'C');
            var sb = new StringBuilder(upper.Length);
            foreach (var ch in upper)
                sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            return Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
        }
    }
}
