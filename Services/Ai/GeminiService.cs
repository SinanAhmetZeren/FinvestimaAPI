using System.Text;
using System.Text.Json;
using FinvestimaAPI.Dtos.AiDtos;

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

        private static readonly string SamplePath = Path.Combine(AppContext.BaseDirectory, "Resources", "sample-statement.png");

        // Hand-verified extraction of Resources/sample-statement.png, used as a few-shot example
        // so Gemini learns the exact rowType classification (title/line/sum) and table shape.
        private const string SampleOutputJson = """
        {
          "statements": [
            {
              "statementType": "Income Statement",
              "columns": ["2012", "2013", "2014", "2015", "2016", "2017"],
              "rows": [
                { "label": "Revenue", "rowType": "line", "values": [102007, 118086, 131345, 142341, 150772, 158311] },
                { "label": "Cost of Goods Sold (COGS)", "rowType": "line", "values": [39023, 48004, 49123, 52654, 56710, 58575] },
                { "label": "Gross Profit", "rowType": "sum", "values": [62984, 70082, 82222, 89687, 94062, 99736] },
                { "label": "Expenses", "rowType": "title", "values": [] },
                { "label": "Salaries and Benefits", "rowType": "line", "values": [26427, 22658, 23872, 23002, 25245, 26913] },
                { "label": "Rent and Overhead", "rowType": "line", "values": [10963, 10125, 10087, 11020, 11412, 10000] },
                { "label": "Depreciation & Amortization", "rowType": "line", "values": [19500, 18150, 17205, 16544, 16080, 15008] },
                { "label": "Interest", "rowType": "line", "values": [2500, 2500, 1500, 1500, 1500, 1500] },
                { "label": "Total Expenses", "rowType": "sum", "values": [59390, 53433, 52664, 52066, 54237, 53421] },
                { "label": "Earnings Before Tax", "rowType": "sum", "values": [3594, 16649, 29558, 37622, 39825, 46314] },
                { "label": "Taxes", "rowType": "line", "values": [1120, 4858, 8483, 10908, 11598, 12968] },
                { "label": "Net Earnings", "rowType": "sum", "values": [2474, 11791, 21075, 26713, 28227, 33346] }
              ],
              "footnotes": []
            },
            {
              "statementType": "Balance Sheet",
              "columns": ["2012", "2013", "2014", "2015", "2016", "2017"],
              "rows": [
                { "label": "Assets", "rowType": "title", "values": [] },
                { "label": "Cash", "rowType": "line", "values": [167971, 181210, 183715, 211069, 239550, 272530] },
                { "label": "Accounts Receivable", "rowType": "line", "values": [5100, 5904, 6567, 7117, 7539, 7807] },
                { "label": "Inventory", "rowType": "line", "values": [7805, 9601, 9825, 10531, 11342, 11715] },
                { "label": "Property & Equipment", "rowType": "line", "values": [45500, 42350, 40145, 38602, 37521, 37513] },
                { "label": "Total Assets", "rowType": "sum", "values": [226376, 239065, 240252, 267319, 295951, 329564] },
                { "label": "Liabilities", "rowType": "title", "values": [] },
                { "label": "Accounts Payable", "rowType": "line", "values": [3902, 4800, 4912, 5265, 5671, 5938] },
                { "label": "Debt", "rowType": "line", "values": [50000, 50000, 30000, 30000, 30000, 30000] },
                { "label": "Total Liabilities", "rowType": "sum", "values": [53902, 54800, 34912, 35265, 35671, 35938] },
                { "label": "Shareholder's Equity", "rowType": "title", "values": [] },
                { "label": "Equity Capital", "rowType": "line", "values": [170000, 170000, 170000, 170000, 170000, 170000] },
                { "label": "Retained Earnings", "rowType": "line", "values": [2474, 14265, 35340, 62053, 90280, 123627] },
                { "label": "Total Shareholder's Equity", "rowType": "sum", "values": [172474, 184265, 205340, 232053, 260280, 293627] },
                { "label": "Total Liabilities & Shareholder's Equity", "rowType": "sum", "values": [226376, 239065, 240252, 267319, 295951, 329564] }
              ],
              "footnotes": []
            },
            {
              "statementType": "Cash Flow Statement",
              "columns": ["2012", "2013", "2014", "2015", "2016", "2017"],
              "rows": [
                { "label": "Operating Cash Flow", "rowType": "title", "values": [] },
                { "label": "Net Earnings", "rowType": "line", "values": [2474, 11791, 21075, 26713, 28227, 33346] },
                { "label": "Plus: Depreciation & Amortization", "rowType": "line", "values": [19500, 18150, 17205, 16544, 16080, 15008] },
                { "label": "Less: Changes in Working Capital", "rowType": "line", "values": [9003, 1702, 775, 903, 827, 375] },
                { "label": "Cash from Operations", "rowType": "sum", "values": [12971, 28239, 37505, 42354, 43480, 47980] },
                { "label": "Investing Cash Flow", "rowType": "title", "values": [] },
                { "label": "Investments in Property & Equipment", "rowType": "line", "values": [15000, 15000, 15000, 15000, 15000, 15000] },
                { "label": "Cash from Investing", "rowType": "sum", "values": [15000, 15000, 15000, 15000, 15000, 15000] },
                { "label": "Financing Cash Flow", "rowType": "title", "values": [] },
                { "label": "Issuance (repayment) of debt", "rowType": "line", "values": [170000, 0, -20000, 0, 0, 0] },
                { "label": "Issuance (repayment) of equity", "rowType": "line", "values": [0, 0, 0, 0, 0, 0] },
                { "label": "Cash from Financing", "rowType": "sum", "values": [170000, 0, -20000, 0, 0, 0] },
                { "label": "Net Increase (decrease) in Cash", "rowType": "sum", "values": [167971, 13239, 2505, 27354, 28480, 32980] },
                { "label": "Opening Cash Balance", "rowType": "line", "values": [0, 167971, 181210, 183715, 211069, 239550] },
                { "label": "Closing Cash Balance", "rowType": "sum", "values": [167971, 181210, 183715, 211069, 239550, 272530] }
              ],
              "footnotes": []
            }
          ]
        }
        """;

        private const string ExtractionPrompt =
            "You will see two documents. The FIRST is a SAMPLE financial document, followed by its correct structured JSON extraction — study this to understand the exact format expected. " +
            "The SECOND is the REAL document you must extract data from, in the same format.\n\n" +
            "For the real document, produce a structured extraction with one entry per financial statement found (e.g. Income Statement, Balance Sheet, Cash Flow Statement). Each statement has:\n" +
            "- statementType: the name of the statement.\n" +
            "- columns: the ordered list of period labels (e.g. years or quarters) as column headers, exactly as shown in the document.\n" +
            "- rows: the rows of the table, in the same top-to-bottom order as they appear in the document. Each row has:\n" +
            "  - label: the row's text label exactly as shown.\n" +
            "  - rowType: 'title' for a section header with no numeric values (e.g. 'Assets', 'Expenses' used purely to group following rows), " +
            "'sum' for a bold/underlined subtotal or total row (e.g. Gross Profit, Total Expenses, Total Assets, Net Earnings), or 'line' for a normal line item.\n" +
            "  - values: the numeric value for each column in order, matching the 'columns' list length. Use null for missing/blank cells. For 'title' rows, use an empty array. " +
            "Negative values or values shown in parentheses must be returned as negative numbers.\n" +
            "- footnotes: any footnotes or notes specific to that statement in the document, each with the note text and a short comment (1-2 sentences) explaining how to interpret it financially. Use an empty array if there are none.\n\n" +
            "Preserve the exact row order and section groupings as they appear in the document.";

        public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
        {
            _httpClient = httpClient;
            _apiKey = configuration["Google_Gemini_Parrots_AI_Query_Key"]
                      ?? throw new ArgumentNullException("Gemini API key is missing.");
            _logger = logger;
        }

        public async Task<string?> AnalyzeFileAsync(byte[] fileBytes, string mimeType, string prompt)
        {
            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(fileBytes) } },
                            new { text = prompt }
                        }
                    }
                }
            };

            return await SendAsync(requestBody);
        }

        private static readonly HashSet<string> TextMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "text/csv", "text/plain", "application/csv", "application/vnd.ms-excel"
        };

        public async Task<FinancialExtractionResultDto?> ExtractFinancialDataAsync(byte[] fileBytes, string mimeType)
        {
            var sampleBytes = await File.ReadAllBytesAsync(SamplePath);

            object realDocumentPart = TextMimeTypes.Contains(mimeType)
                ? new { text = "```csv\n" + Encoding.UTF8.GetString(fileBytes) + "\n```" }
                : new { inline_data = new { mime_type = mimeType, data = Convert.ToBase64String(fileBytes) } };

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new object[]
                        {
                            new { text = "SAMPLE document:" },
                            new { inline_data = new { mime_type = "image/png", data = Convert.ToBase64String(sampleBytes) } },
                            new { text = "Correct structured JSON extraction for the sample document above:\n" + SampleOutputJson },
                            new { text = "Now here is the REAL document to extract:" },
                            realDocumentPart,
                            new { text = ExtractionPrompt }
                        }
                    }
                },
                generationConfig = new
                {
                    responseMimeType = "application/json",
                    responseSchema = new
                    {
                        type = "OBJECT",
                        properties = new
                        {
                            statements = new
                            {
                                type = "ARRAY",
                                items = new
                                {
                                    type = "OBJECT",
                                    properties = new
                                    {
                                        statementType = new { type = "STRING" },
                                        columns = new { type = "ARRAY", items = new { type = "STRING" } },
                                        rows = new
                                        {
                                            type = "ARRAY",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    label = new { type = "STRING" },
                                                    rowType = new { type = "STRING", @enum = new[] { "title", "line", "sum" } },
                                                    values = new { type = "ARRAY", items = new { type = "NUMBER", nullable = true } }
                                                },
                                                required = new[] { "label", "rowType", "values" }
                                            }
                                        },
                                        footnotes = new
                                        {
                                            type = "ARRAY",
                                            items = new
                                            {
                                                type = "OBJECT",
                                                properties = new
                                                {
                                                    note = new { type = "STRING" },
                                                    comment = new { type = "STRING" }
                                                },
                                                required = new[] { "note", "comment" }
                                            }
                                        }
                                    },
                                    required = new[] { "statementType", "columns", "rows", "footnotes" }
                                }
                            }
                        },
                        required = new[] { "statements" }
                    }
                }
            };

            var text = await SendAsync(requestBody);
            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                return JsonSerializer.Deserialize<FinancialExtractionResultDto>(text, CaseInsensitiveOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse Gemini structured extraction response: {Text}", text);
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
