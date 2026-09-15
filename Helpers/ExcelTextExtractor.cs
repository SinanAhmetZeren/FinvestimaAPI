using System.Text;
using NPOI.SS.UserModel;
using NPOI.HSSF.UserModel;
using NPOI.XSSF.UserModel;

namespace FinvestimaAPI.Helpers
{
    public static class ExcelTextExtractor
    {
        public static string ExtractAsText(byte[] fileBytes, string fileName)
        {
            using var stream = new MemoryStream(fileBytes);
            IWorkbook workbook = fileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)
                ? new HSSFWorkbook(stream)
                : new XSSFWorkbook(stream);

            var sb = new StringBuilder();

            for (int sheetIndex = 0; sheetIndex < workbook.NumberOfSheets; sheetIndex++)
            {
                var sheet = workbook.GetSheetAt(sheetIndex);
                sb.AppendLine($"Sheet: {sheet.SheetName}");

                for (int rowIndex = sheet.FirstRowNum; rowIndex <= sheet.LastRowNum; rowIndex++)
                {
                    var row = sheet.GetRow(rowIndex);
                    if (row == null) continue;

                    var cells = new List<string>();
                    for (int cellIndex = 0; cellIndex < row.LastCellNum; cellIndex++)
                    {
                        var cell = row.GetCell(cellIndex);
                        cells.Add(FormatCell(cell));
                    }

                    if (cells.Any(c => !string.IsNullOrWhiteSpace(c)))
                        sb.AppendLine(string.Join(",", cells));
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string FormatCell(ICell? cell)
        {
            if (cell == null) return "";

            return cell.CellType switch
            {
                CellType.String => cell.StringCellValue,
                CellType.Numeric => DateUtil.IsCellDateFormatted(cell)
                    ? cell.DateCellValue?.ToString("yyyy-MM-dd") ?? ""
                    : cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                CellType.Boolean => cell.BooleanCellValue.ToString(),
                CellType.Formula => TryGetFormulaResult(cell),
                _ => "",
            };
        }

        private static string TryGetFormulaResult(ICell cell)
        {
            try
            {
                return cell.CachedFormulaResultType switch
                {
                    CellType.Numeric => cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    CellType.String => cell.StringCellValue,
                    _ => "",
                };
            }
            catch
            {
                return "";
            }
        }
    }
}
