using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;

namespace FinvestimaAPI.Helpers
{
    /// <summary>
    /// Simple, dependency-light helpers for stripping logos that are known to sit only in the
    /// top or bottom margin of a page/image: a horizontal white-band redaction, plus a
    /// whitespace-gap heuristic used to suggest where those bands should go.
    /// Intentionally does NOT attempt real logo/object detection.
    /// </summary>
    public static class LogoRedactionHelper
    {
        private const double DefaultSuggestedPercent = 8.0;
        private const double ScanZoneFraction = 0.25; // only look within the top/bottom 25% of the image
        private const int BlankRowThreshold = 250; // average luma above this counts as "near-blank"

        public static int GetPdfPageCount(byte[] pdfBytes)
        {
            return Conversion.GetPageCount(pdfBytes, password: null);
        }

        public static byte[] RasterizePdfFirstPageToPng(byte[] pdfBytes, int dpi = 150)
        {
            using var bitmap = Conversion.ToImage(pdfBytes, page: 0, password: null, options: new RenderOptions(Dpi: dpi));
            using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        public static List<byte[]> RasterizePdfAllPagesToPng(byte[] pdfBytes, int dpi = 150)
        {
            var results = new List<byte[]>();
            foreach (var bitmap in Conversion.ToImages(pdfBytes, password: null, options: new RenderOptions(Dpi: dpi)))
            {
                using (bitmap)
                using (var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
                {
                    results.Add(data.ToArray());
                }
            }
            return results;
        }

        /// <summary>
        /// Rasterizes only the given 0-based page indices, in the order supplied, so a user can
        /// select a subset of a multi-page PDF (a "print dialog"-style page picker) instead of
        /// always sending every page to Gemini.
        /// </summary>
        public static List<byte[]> RasterizePdfPagesToPng(byte[] pdfBytes, IEnumerable<int> pageIndices, int dpi = 150)
        {
            var results = new List<byte[]>();
            foreach (var pageIndex in pageIndices)
            {
                using var bitmap = Conversion.ToImage(pdfBytes, page: pageIndex, password: null, options: new RenderOptions(Dpi: dpi));
                using var data = bitmap.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
                results.Add(data.ToArray());
            }
            return results;
        }

        /// <summary>
        /// Loads the given image bytes and returns a base64 PNG plus suggested topPercent/bottomPercent
        /// for where a logo band likely sits, based on a whitespace-gap scan.
        /// </summary>
        public static (string base64Png, double topPercent, double bottomPercent) AnalyzeForPreview(byte[] imageBytes)
        {
            using var image = Image.Load<Rgba32>(imageBytes);

            var rowLuma = ComputeRowAverageLuma(image);
            var topPercent = FindTopGapPercent(rowLuma, image.Height);
            var bottomPercent = FindBottomGapPercent(rowLuma, image.Height);

            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            var base64 = Convert.ToBase64String(ms.ToArray());

            return (base64, topPercent, bottomPercent);
        }

        private static double[] ComputeRowAverageLuma(Image<Rgba32> image)
        {
            int width = image.Width;
            int height = image.Height;
            var rowLuma = new double[height];

            image.ProcessPixelRows(accessor =>
            {
                for (int y = 0; y < accessor.Height; y++)
                {
                    var rowSpan = accessor.GetRowSpan(y);
                    long sum = 0;
                    for (int x = 0; x < rowSpan.Length; x++)
                    {
                        var p = rowSpan[x];
                        sum += (int)(0.299 * p.R + 0.587 * p.G + 0.114 * p.B);
                    }
                    rowLuma[y] = width == 0 ? 255 : (double)sum / width;
                }
            });

            return rowLuma;
        }

        private static double FindTopGapPercent(double[] rowLuma, int height)
        {
            int zoneEnd = Math.Max(1, (int)(height * ScanZoneFraction));

            bool inGap = false;
            int gapStart = -1;
            int bestGapEnd = -1;
            int bestGapLen = 0;

            for (int y = 0; y < zoneEnd; y++)
            {
                bool isBlank = rowLuma[y] >= BlankRowThreshold;
                if (isBlank && !inGap)
                {
                    inGap = true;
                    gapStart = y;
                }
                else if (!isBlank && inGap)
                {
                    inGap = false;
                    int gapLen = y - gapStart;
                    // Ignore a gap that starts at row 0 (that's just blank page margin, not a
                    // "gap after content" marker) unless it's the only thing we find.
                    if (gapStart > 0 && gapLen > bestGapLen)
                    {
                        bestGapLen = gapLen;
                        bestGapEnd = y;
                    }
                }
            }

            if (bestGapEnd <= 0)
                return DefaultSuggestedPercent;

            return Math.Clamp((double)bestGapEnd / height * 100.0, 1.0, ScanZoneFraction * 100.0);
        }

        private static double FindBottomGapPercent(double[] rowLuma, int height)
        {
            int zoneStart = Math.Max(0, height - (int)(height * ScanZoneFraction));

            bool inGap = false;
            int gapStart = -1;
            int bestGapStartFromBottom = -1;
            int bestGapLen = 0;

            for (int y = height - 1; y >= zoneStart; y--)
            {
                bool isBlank = rowLuma[y] >= BlankRowThreshold;
                if (isBlank && !inGap)
                {
                    inGap = true;
                    gapStart = y;
                }
                else if (!isBlank && inGap)
                {
                    inGap = false;
                    int gapLen = gapStart - y;
                    if (gapStart < height - 1 && gapLen > bestGapLen)
                    {
                        bestGapLen = gapLen;
                        bestGapStartFromBottom = height - 1 - y;
                    }
                }
            }

            if (bestGapStartFromBottom <= 0)
                return DefaultSuggestedPercent;

            return Math.Clamp((double)bestGapStartFromBottom / height * 100.0, 1.0, ScanZoneFraction * 100.0);
        }

        /// <summary>
        /// Paints solid-white rectangles over the top topPercent% and bottom bottomPercent% of the
        /// image's height, then re-encodes as PNG.
        /// </summary>
        public static byte[] RedactBands(byte[] imageBytes, double topPercent, double bottomPercent, double leftPercent = 0, double rightPercent = 0)
        {
            using var image = Image.Load<Rgba32>(imageBytes);

            int width = image.Width;
            int height = image.Height;
            int topHeight = (int)Math.Round(height * Math.Clamp(topPercent, 0, 100) / 100.0);
            int bottomHeight = (int)Math.Round(height * Math.Clamp(bottomPercent, 0, 100) / 100.0);
            int leftWidth = (int)Math.Round(width * Math.Clamp(leftPercent, 0, 100) / 100.0);
            int rightWidth = (int)Math.Round(width * Math.Clamp(rightPercent, 0, 100) / 100.0);

            topHeight = Math.Clamp(topHeight, 0, height);
            bottomHeight = Math.Clamp(bottomHeight, 0, height - topHeight);
            leftWidth = Math.Clamp(leftWidth, 0, width);
            rightWidth = Math.Clamp(rightWidth, 0, width - leftWidth);

            image.Mutate(ctx =>
            {
                if (topHeight > 0)
                    ctx.Fill(Color.White, new Rectangle(0, 0, width, topHeight));

                if (bottomHeight > 0)
                    ctx.Fill(Color.White, new Rectangle(0, height - bottomHeight, width, bottomHeight));

                if (leftWidth > 0)
                    ctx.Fill(Color.White, new Rectangle(0, 0, leftWidth, height));

                if (rightWidth > 0)
                    ctx.Fill(Color.White, new Rectangle(width - rightWidth, 0, rightWidth, height));
            });

            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            return ms.ToArray();
        }

        /// <summary>
        /// Parses a print-dialog-style page selection string (e.g. "1-3,5", "2", " 1, 4-6 ")
        /// into a distinct, ascending list of 0-based page indices, clamped to
        /// [0, pageCount). Returns null for a null/empty/"all" input, meaning "every page".
        /// Malformed tokens are ignored rather than throwing, since this only affects which
        /// pages get sent for extraction — never a security- or correctness-critical parse.
        /// </summary>
        public static List<int>? ParsePageSelection(string? input, int pageCount)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Trim().Equals("all", StringComparison.OrdinalIgnoreCase))
                return null;

            var indices = new SortedSet<int>();
            foreach (var rawToken in input.Split(','))
            {
                var token = rawToken.Trim();
                if (token.Length == 0) continue;

                var dash = token.IndexOf('-');
                if (dash > 0)
                {
                    var startPart = token[..dash].Trim();
                    var endPart = token[(dash + 1)..].Trim();
                    if (int.TryParse(startPart, out var start) && int.TryParse(endPart, out var end))
                    {
                        if (start > end) (start, end) = (end, start);
                        for (var p = start; p <= end; p++)
                        {
                            if (p >= 1 && p <= pageCount) indices.Add(p - 1);
                        }
                    }
                }
                else if (int.TryParse(token, out var single))
                {
                    if (single >= 1 && single <= pageCount) indices.Add(single - 1);
                }
            }

            return indices.Count > 0 ? indices.ToList() : null;
        }
    }
}
