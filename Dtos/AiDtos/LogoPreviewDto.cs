namespace FinvestimaAPI.Dtos.AiDtos
{
    public class LogoPreviewDto
    {
        public string PreviewImageBase64 { get; set; } = string.Empty;
        public double SuggestedTopPercent { get; set; }
        public double SuggestedBottomPercent { get; set; }
        public int PageCount { get; set; } = 1;
        public int PreviewPage { get; set; } = 1;
    }
}
