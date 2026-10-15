namespace FinvestimaAPI.Models.ValuatorPro
{
    // Bir ValuationTask için TEK kayıt — farklı dosyalardan gelen aylar hepsi buraya
    // birikir (bkz. MonthlySalesRow.Year/Month), dosya başına ayrı obje açılmaz.
    public class MonthlySalesObject
    {
        public Guid Id { get; set; }
        public Guid ValuationTaskId { get; set; }
        public ValuationTask? ValuationTask { get; set; }
        public string? FileName { get; set; }
        public bool IsRemoved { get; set; }
        public ExtractionJobStatus Status { get; set; } = ExtractionJobStatus.Pending;
        public string? ErrorMessage { get; set; }
        public List<MonthlySalesRow> MonthlySalesRows { get; set; } = new();
    }
}
