using System.Text;
using System.Text.Json;
using FinvestimaAPI.Dtos.ValuatorProDtos;

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

        private static readonly HashSet<string> TextMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "text/csv", "text/plain", "application/csv", "application/vnd.ms-excel"
        };

        private static readonly string[] MizanModelPaths =
        {
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "Firma.cs"),
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "MizanYil.cs"),
            Path.Combine(AppContext.BaseDirectory, "Models", "ValuatorPro", "MizanHesap.cs"),
        };

        private const string MizanExtractionPrompt =
            "You are extracting mizan (Turkish trial balance) rows from the REAL document below into " +
            "the C# model shapes shown above (Firma/MizanYil/MizanHesap). Rules:\n\n" +
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
            "if present (e.g. \"01/01/2025-31/12/2025 ARASI MİZAN\").";

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Google_Gemini_Parrots_AI_Query_Key"]
                      ?? throw new ArgumentNullException("Gemini API key is missing.");
            _logger = logger;
        }

        public async Task<MizanExtractionResultDto?> ExtractMizanAsync(byte[] fileBytes, string mimeType, MizanExtractionResultDto? currentSnapshot)
        {
            var parts = new List<object>
            {
                new { text = "Tekdüzen Hesap Planı (chart of accounts) reference:\n" + await File.ReadAllTextAsync(TekduzenReferencePath) },
                new { text = "Target C# model shapes (Firma/MizanYil/MizanHesap):\n\n" + string.Join("\n\n", MizanModelPaths.Select(p => File.Exists(p) ? File.ReadAllText(p) : string.Empty)) },
            };

            if (currentSnapshot != null && currentSnapshot.MizanYillar.Count > 0)
            {
                parts.Add(new { text = "Current snapshot already on file (context only, do not repeat):\n" + JsonSerializer.Serialize(currentSnapshot) });
            }

            parts.Add(new { text = "Now here is the REAL document to extract:" });
            parts.Add(TextMimeTypes.Contains(mimeType)
                ? new { text = "```csv\n" + Encoding.UTF8.GetString(fileBytes) + "\n```" }
                : (object)new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(fileBytes) } });
            parts.Add(new { text = MizanExtractionPrompt });

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
                            mizanYillar = new
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
                                        mizanHesaplar = new
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
                                                    creditBalance = new { type = "NUMBER" }
                                                },
                                                required = new[] { "code", "name", "debit", "credit", "debitBalance", "creditBalance" }
                                            }
                                        }
                                    },
                                    required = new[] { "year", "months", "isPartial", "mizanHesaplar" }
                                }
                            }
                        },
                        required = new[] { "mizanYillar" }
                    }
                }
            };

            var text = await SendAsync(requestBody);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                return JsonSerializer.Deserialize<MizanExtractionResultDto>(text, CaseInsensitiveOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini mizan extraction response: {Text}", text);
                return null;
            }
        }

        private async Task<string?> SendAsync(object requestBody)
        {
            var json = JsonSerializer.Serialize(requestBody);

            foreach (var model in Models)
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
                    using var doc = JsonDocument.Parse(responseJson);

                    if (doc.RootElement.TryGetProperty("candidates", out var candidates) &&
                        candidates.GetArrayLength() > 0 &&
                        candidates[0].TryGetProperty("content", out var candidateContent) &&
                        candidateContent.TryGetProperty("parts", out var parts) &&
                        parts.GetArrayLength() > 0)
                    {
                        return parts[0].GetProperty("text").GetString();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception calling Gemini API on model {Model}", model);
                }
            }

            return null;
        }
    }
}
