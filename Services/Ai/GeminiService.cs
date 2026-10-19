using System.Text;
using System.Text.Json;
using FinvestimaAPI.Dtos.ValuatorProDtos;
using FinvestimaAPI.Models.ValuatorPro;

namespace FinvestimaAPI.Services.Ai
{
    public class GeminiService : IGeminiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;

        private static readonly string[] Models =
        {
            "gemini-flash-lite-latest",
            "gemini-flash-latest"
        };

        private static readonly JsonSerializerOptions CaseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

        private static readonly string TekduzenReferencePath = Path.Combine(AppContext.BaseDirectory, "Resources", "tekduzen-hesap-plani.txt");
        private static readonly string UsGaapReferencePath = Path.Combine(AppContext.BaseDirectory, "Resources", "us-gaap-chart.txt");

        private static readonly HashSet<string> TextMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "text/csv", "text/plain", "application/csv", "application/vnd.ms-excel"
        };

        private static readonly string[] TrialBalanceModelPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "TrialBalanceYear.cs"),
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "TrialBalanceAccount.cs"),
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "MonthlySalesRow.cs"),
        };

        private const string TrialBalanceExtractionPrompt =
            "The REAL document below may contain a mizan (Turkish trial balance), a monthly sales report, " +
            "or both (as separate sheets/sections of the same file). Extract whichever is actually present " +
            "into the matching part of the response — do not fabricate data for a kind of document that " +
            "isn't there, and populate BOTH trialBalanceYears and monthlySalesRows if both are present.\n\n" +
            "MIZAN rows (into trialBalanceYears, matching the TrialBalanceYear/TrialBalanceAccount C# shapes above):\n" +
            "1. A parent account code (e.g. \"100\") does not have to appear together with its sub-codes " +
            "(e.g. \"100 01\", \"100 01 01 001\"). Either may appear alone, depending on how the mizan was " +
            "exported. Do NOT assume the hierarchy is complete and do NOT fabricate a missing parent or " +
            "child row that isn't actually printed in the document — extract only the exact code rows that " +
            "appear in the file, as-is.\n" +
            "2. At any level there can be multiple sibling codes (e.g. under \"100 01\" there can be both " +
            "\"100 01 01\" and \"100 01 02\").\n" +
            "3. Do not aggregate codes to the 3-digit level yourself — return the Code exactly as printed in " +
            "the document (whatever its depth), nothing else.\n" +
            "4. A CURRENT SNAPSHOT of years already on file may be provided below purely as CONTEXT, so you " +
            "correctly identify which year(s) the new document covers and avoid misreading it. Do NOT repeat " +
            "snapshot years in your response. Your response must contain ONLY the year(s) actually present in " +
            "the REAL document provided now.\n" +
            "5. Use the attached chart-of-accounts reference to resolve account names/codes when the document " +
            "is ambiguous or abbreviated, but always prefer the code/name exactly as printed in the real " +
            "document when they differ.\n" +
            "6. months is 12 for a full calendar year; set it lower and isPartial=true for a partial-year " +
            "mizan (e.g. a current, not-yet-closed year). label should be the period string from the document " +
            "if present (e.g. \"01/01/2025-31/12/2025 ARASI MİZAN\").\n\n" +
            "MONTHLY SALES rows (into monthlySalesRows) — a table with one row per year+month, typically " +
            "with columns like Yıl/Ay/Dönem, TL Ciro, USD Ciro, Hammadde/Malzeme maliyeti, OPEX. Column names " +
            "and which columns are present vary between files — infer the right ones by meaning, not by exact " +
            "header text:\n" +
            "1. One row per (year, month) actually present in the document — do not fabricate missing months.\n" +
            "2. revenue/materialCost/opex should be the TL (TRY) figures; set currency=\"TRY\". If the document " +
            "only has a foreign-currency figure (e.g. USD) and no TL figure for a value, leave that numeric " +
            "field 0 rather than guessing a conversion — do not convert currencies yourself.\n" +
            "3. If a column (materialCost or opex) is simply absent from the document, return 0 for it rather " +
            "than fabricating a value.";

        // GAAP'ta MIZAN kuralı #5'in yerini alır: artık "Tekdüzen referansıyla belirsizliği çöz" değil,
        // "şirketin kendi, standart olmayan hesap adlarını bu gölge tabloya eşle" talimatı veriliyor.
        private const string UsGaapCodeMappingInstruction =
            "5. This company does NOT use the attached chart of accounts — it is a shadow/internal " +
            "reference table only, not the company's real coding. The real document will have its OWN " +
            "arbitrary account codes and/or names (e.g. from QuickBooks, NetSuite, a custom chart). For " +
            "EVERY account row: (a) read the account's code AND/OR name exactly as printed in the real " +
            "document and put them, unchanged, into originalCode and originalName; (b) based on the " +
            "MEANING of that account (not any numeric similarity), classify it into the single best-fitting " +
            "entry in the attached shadow chart, and put THAT entry's code into code and THAT entry's name " +
            "into name. If the document has no code at all (name-only), leave originalCode empty and put the " +
            "account name in originalName. Never invent an originalCode/originalName that isn't in the document.";

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Google_Gemini_Parrots_AI_Query_Key"]
                      ?? throw new ArgumentNullException("Gemini API key is missing.");
            _logger = logger;
        }

        public async Task<TrialBalanceExtractionResultDto?> ExtractTrialBalanceAsync(byte[] fileBytes, string mimeType, TrialBalanceExtractionResultDto? currentSnapshot, AccountingStandard accountingStandard)
        {
            var isGaap = accountingStandard == AccountingStandard.UsGaap;
            var referencePath = isGaap ? UsGaapReferencePath : TekduzenReferencePath;
            var referenceLabel = isGaap ? "Shadow US-GAAP chart of accounts reference (internal — not the company's own coding)" : "Tekdüzen Hesap Planı (chart of accounts) reference";

            var parts = new List<object>
            {
                new { text = referenceLabel + ":\n" + await File.ReadAllTextAsync(referencePath) },
                new { text = "Target C# model shapes (TrialBalanceYear/TrialBalanceAccount/MonthlySalesRow):\n\n" + string.Join("\n\n", TrialBalanceModelPaths.Select(p => File.Exists(p) ? File.ReadAllText(p) : string.Empty)) },
            };

            if (currentSnapshot != null && currentSnapshot.TrialBalanceYears.Count > 0)
            {
                parts.Add(new { text = "Current snapshot already on file (context only, do not repeat):\n" + JsonSerializer.Serialize(currentSnapshot) });
            }

            parts.Add(new { text = "Now here is the REAL document to extract:" });
            parts.Add(TextMimeTypes.Contains(mimeType)
                ? new { text = "```csv\n" + Encoding.UTF8.GetString(fileBytes) + "\n```" }
                : (object)new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(fileBytes) } });
            var prompt = isGaap
                ? TrialBalanceExtractionPrompt.Replace(
                    "5. Use the attached chart-of-accounts reference to resolve account names/codes when the document " +
                    "is ambiguous or abbreviated, but always prefer the code/name exactly as printed in the real " +
                    "document when they differ.\n",
                    UsGaapCodeMappingInstruction + "\n")
                : TrialBalanceExtractionPrompt;
            parts.Add(new { text = prompt });

            var requestBody = new
            {
                contents = new[] { new { parts = parts.ToArray() } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            trialBalanceYears = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        year = new { type = "INTEGER" },
                                        months = new { type = "INTEGER" },
                                        isPartial = new { type = "BOOLEAN" },
                                        fileDebitTotal = new { type = "NUMBER", nullable = true },
                                        fileCreditTotal = new { type = "NUMBER", nullable = true },
                                        label = new { type = "STRING", nullable = true },
                                        trialBalanceAccounts = new
                                        {
                                            type = "ARRAY",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    code = new { type = "STRING" },
                                                    name = new { type = "STRING" },
                                                    debit = new { type = "NUMBER" },
                                                    credit = new { type = "NUMBER" },
                                                    debitBalance = new { type = "NUMBER" },
                                                    creditBalance = new { type = "NUMBER" },
                                                    originalCode = new { type = "STRING", nullable = true },
                                                    originalName = new { type = "STRING", nullable = true }
                                                },
                                                required = new[] { "code", "name", "debit", "credit", "debitBalance", "creditBalance" }
                                            }
                                        }
                                    },
                                    required = new[] { "year", "months", "isPartial", "trialBalanceAccounts" }
                                }
                            },
                            monthlySalesRows = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        year = new { type = "INTEGER" },
                                        month = new { type = "INTEGER" },
                                        currency = new { type = "STRING" },
                                        revenue = new { type = "NUMBER" },
                                        materialCost = new { type = "NUMBER" },
                                        opex = new { type = "NUMBER" }
                                    },
                                    required = new[] { "year", "month", "currency", "revenue", "materialCost", "opex" }
                                }
                            }
                        },
                        required = new[] { "trialBalanceYears", "monthlySalesRows" }
                    }
                }
            };

            var text = await SendAsync(requestBody);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                return JsonSerializer.Deserialize<TrialBalanceExtractionResultDto>(text, CaseInsensitiveOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini trial balance extraction response: {Text}", text);
                return null;
            }
        }

        private const string ComparableMultiplesPromptTemplate =
            "Generate a comprehensive valuation and M&A multiples map for an unlisted company operating " +
            "in the {SECTOR} sector, with revenues of approximately {SALES} USD, based in {COUNTRY} " +
            "({REGION}).\n\n" +
            "The analysis should cover a broad size spectrum—from micro-enterprises to regional and " +
            "global mega-players—benchmarking similar-sized peers as well as small, mid-cap, and " +
            "large-cap entities across Turkey, regional (emerging), and global markets.\n\n" +
            "Present the findings as a comparative matrix categorized by size tiers, drawing on ALL relevant " +
            "corporate finance transactions and valuation benchmarks, explicitly including: (1) public " +
            "trading multiples — EV/EBITDA derived from the current market price of publicly listed, " +
            "exchange-traded peer companies; (2) M&A transactions — disclosed full or majority-stake " +
            "acquisitions; and (3) other corporate finance transactions — minority share sales, private " +
            "equity/VC investment rounds, and similar disclosed deals. For every data point, specify the " +
            "deal/data vintage (year).\n\n" +
            "EVERY row must be anchored to a specific, named real company (or a specific disclosed " +
            "transaction between named companies) — never a generic, unnamed tier-level summary row (e.g. " +
            "do NOT produce a row like \"Tier 2: Small-Cap (Public Trading)\" with no company named; " +
            "instead produce one row per actual company you found, such as \"Company X, EV/EBITDA 6.2x\"). " +
            "If you cannot find enough named companies/deals to cover a size tier, it is fine to have fewer " +
            "rows for that tier — do not fill the gap with an unnamed estimate row.\n\n" +
            "For sourceBasis: name a specific, real, independently verifiable source (a named index " +
            "provider, exchange, research firm, or a specific disclosed transaction) ONLY if you are " +
            "actually confident it exists and covers this data point — never invent a plausible-sounding " +
            "database, index, or report name. If you are not confident of a specific real source, say so " +
            "plainly (e.g. \"model estimate based on general sector knowledge, no specific source\") and " +
            "set isEstimate=true. A vague or fabricated-sounding citation is worse than an honest estimate. " +
            "Prefix sourceBasis with which of the three categories above the data point belongs to, in " +
            "parentheses — e.g. \"(Public trading) ...\", \"(M&A transaction) ...\", or \"(Other corporate " +
            "finance transaction) ...\". For a public trading multiple, ALWAYS name the specific listed " +
            "company (or companies) the multiple is based on, not just the exchange/index — e.g. \"(Public " +
            "trading) Brembo N.V., Euronext Milan\" rather than just \"(Public trading) Euronext Milan " +
            "Market Data\". Likewise for an M&A or other transaction, name the specific company/companies " +
            "involved whenever you are confident of them. When a data point is a specific disclosed " +
            "transaction and you are confident of a real public URL for it (a press release, filing, or " +
            "news article), include that URL in sourceUrl; otherwise leave sourceUrl empty — never " +
            "fabricate a URL.";

        public async Task<List<ComparableMultiplesRowDto>?> GetComparableMultiplesAsync(ComparableMultiplesRequestDto request)
        {
            var prompt = ComparableMultiplesPromptTemplate
                .Replace("{SECTOR}", request.SectorSubSector)
                .Replace("{SALES}", request.YearlySales.ToString("N0"))
                .Replace("{COUNTRY}", request.Country)
                .Replace("{REGION}", request.Region);
            if (request.Language == "tr")
                prompt += "\n\nRespond in Turkish — category and sourceBasis text should be in Turkish " +
                          "(numbers, codes and URLs stay as-is).";

            // Gemini'nin JSON modu (responseSchema) ile google_search aracı aynı istekte birlikte
            // kullanılamıyor — önce arama yapan serbest metin bir istek atılıyor (gerçek kaynak/URL
            // bulsun diye), sonra o metin ikinci bir istekte (schema ile, arama olmadan) JSON'a
            // dönüştürülüyor. Böylece JSON satırları uydurma değil, gerçekten arama sonucu olur.
            var researchBody = new
            {
                contents = new[] { new { parts = new object[] { new { text = prompt } } } },
                tools = new object[] { new { googleSearch = new { } } },
            };
            // Flash-lite'ın araştırma/kaynak muhakemesi zayıf — bu çağrıda sadece daha güçlü model
            // kullanılıyor, lite'a düşülmüyor (ikinci, yapılandırma adımı lite ile de yeterli).
            var (researchText, sources) = await SendGroundedAsync(researchBody, new[] { "gemini-flash-latest" });
            if (string.IsNullOrWhiteSpace(researchText))
                return null;

            // Model'in metin içine yazdığı URL'lere güvenmek yerine, Google Search'ün asıl döndürdüğü
            // groundingMetadata kaynak listesi ayrıca veriliyor — sourceUrl SADECE buradan seçilebilir.
            var sourcesBlock = sources.Count > 0
                ? "\n\nSOURCES ACTUALLY RETURNED BY SEARCH (sourceUrl may ONLY be chosen from this list, matched " +
                  "to the relevant row; if no source here fits a row, leave that row's sourceUrl empty):\n" +
                  string.Join("\n", sources.Select(s => $"- {s.Title}: {s.Uri}"))
                : "\n\nNo web sources were returned by search — sourceUrl must be empty for every row.";

            var structurePrompt =
                "Structure the research notes below into the required JSON shape — one row per size " +
                "tier/category.\n" +
                "For sourceBasis: describe the source or logic mentioned in the research text, keeping the " +
                "leading \"(Public trading)\"/\"(M&A transaction)\"/\"(Other corporate finance transaction)\" " +
                "category prefix if the research notes include one, and keeping the specific company " +
                "name(s) mentioned rather than dropping them for just the exchange/index name.\n" +
                "For sourceUrl: select the exact matching URL ONLY from the 'SOURCES ACTUALLY RETURNED BY " +
                "SEARCH' list below. If no URL in that list corresponds to a row, set sourceUrl to null and " +
                "isEstimate=true." +
                (request.Language == "tr" ? " category and sourceBasis text must be in Turkish." : "") +
                "\n\nRESEARCH NOTES:\n" + researchText + sourcesBlock;

            var requestBody = new
            {
                contents = new[] { new { parts = new object[] { new { text = structurePrompt } } } },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            rows = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        category = new { type = "STRING" },
                                        rangeLow = new { type = "NUMBER" },
                                        rangeHigh = new { type = "NUMBER" },
                                        median = new { type = "NUMBER" },
                                        vintage = new { type = "STRING" },
                                        sourceBasis = new { type = "STRING" },
                                        sourceUrl = new { type = "STRING", nullable = true },
                                        isEstimate = new { type = "BOOLEAN" }
                                    },
                                    required = new[] { "category", "rangeLow", "rangeHigh", "median", "vintage", "sourceBasis", "isEstimate" }
                                }
                            }
                        },
                        required = new[] { "rows" }
                    }
                }
            };

            var text = await SendAsync(requestBody);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                var parsed = JsonSerializer.Deserialize<ComparableMultiplesResultDto>(text, CaseInsensitiveOptions);
                if (parsed?.Rows != null)
                {
                    // Prompt uyumu garanti değil — model listede olmayan bir URL döndürürse sessizce
                    // null'a çekilir, hiçbir zaman olduğu gibi kullanıcıya gösterilmez.
                    var validUrls = new HashSet<string>(sources.Select(s => s.Uri), StringComparer.OrdinalIgnoreCase);
                    foreach (var row in parsed.Rows)
                        if (!string.IsNullOrEmpty(row.SourceUrl) && !validUrls.Contains(row.SourceUrl))
                            row.SourceUrl = null;
                }
                return parsed?.Rows;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini comparable multiples response: {Text}", text);
                return null;
            }
        }

        private async Task<string?> SendAsync(object requestBody, string[]? models = null)
        {
            var json = JsonSerializer.Serialize(requestBody);
            Console.WriteLine($"[GeminiService] Request payload size: {json.Length} chars (~{json.Length/1024}KB)");

            foreach (var model in models ?? Models)
            {
                try
                {
                    Console.WriteLine($"[GeminiService] Sending request to model '{model}'...");
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
                    var response = await _httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"[GeminiService] Model '{model}' failed ({response.StatusCode}): {error}");
                        _logger.LogWarning("Gemini warning on model '{Model}' ({Status}): {Error}", model, response.StatusCode, error);
                        continue;
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[GeminiService] Model '{model}' responded successfully ({responseJson.Length} chars)");
                    await SaveRawResponseAsync(model, responseJson);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0)
                    {
                        if (candidates[0].TryGetProperty("finishReason", out var fr))
                            Console.WriteLine($"[GeminiService] finishReason: {fr.GetString()}");

                        if (candidates[0].TryGetProperty("content", out var candidateContent) &&
                            candidateContent.TryGetProperty("parts", out var parts) &&
                            parts.GetArrayLength() > 0)
                        {
                            return parts[0].GetProperty("text").GetString();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception calling Gemini API on model {Model}", model);
                }
            }

            return null;
        }

        // googleSearch aracıyla yapılan çağrılar için — SendAsync'ten farkı, sadece üretilen metni
        // değil, Google Search'ün gerçekten döndürdüğü groundingMetadata kaynak listesini de
        // (başlık + gerçek URL) ayrıca döndürür. Model metninin içine yazdığı URL'lere güvenmek
        // yerine bu liste, ikinci (yapılandırma) adımda sourceUrl'nin tek doğru kaynağıdır.
        private async Task<(string? Text, List<(string Title, string Uri)> Sources)> SendGroundedAsync(object requestBody, string[] models)
        {
            var json = JsonSerializer.Serialize(requestBody);
            foreach (var model in models)
            {
                try
                {
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={_apiKey}";
                    var response = await _httpClient.PostAsync(url, content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        _logger.LogWarning("Gemini warning on model '{Model}' ({Status}): {Error}", model, response.StatusCode, error);
                        continue;
                    }

                    var responseJson = await response.Content.ReadAsStringAsync();
                    await SaveRawResponseAsync(model, responseJson);
                    using var doc = JsonDocument.Parse(responseJson);

                    if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
                        continue;
                    var cand = candidates[0];

                    string? text = null;
                    if (cand.TryGetProperty("content", out var cc) && cc.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                        text = parts[0].GetProperty("text").GetString();

                    var sources = new List<(string, string)>();
                    if (cand.TryGetProperty("groundingMetadata", out var gm) && gm.TryGetProperty("groundingChunks", out var chunks))
                    {
                        foreach (var chunk in chunks.EnumerateArray())
                        {
                            if (chunk.TryGetProperty("web", out var web))
                            {
                                var title = web.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                var uri = web.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "";
                                if (!string.IsNullOrEmpty(uri)) sources.Add((title, uri));
                            }
                        }
                    }

                    if (text != null) return (text, sources);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception calling Gemini API on model {Model}", model);
                }
            }

            return (null, new List<(string, string)>());
        }

        // Gemini'den dönen ham cevabı (parse edilmeden önce) logs/gemini-responses altına yazar —
        // parse hatası olduğunda terminale yapıştırmaya gerek kalmadan doğrudan dosyadan incelenir.
        private static async Task SaveRawResponseAsync(string model, string responseJson)
        {
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "logs", "gemini-responses");
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{model}.json");
                await File.WriteAllTextAsync(path, responseJson);
                Console.WriteLine($"[GeminiService] Raw response saved to {Path.GetFullPath(path)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GeminiService] Failed to save raw response: {ex.Message}");
            }
        }
    }
}
