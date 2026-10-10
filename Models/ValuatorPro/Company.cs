namespace FinvestimaAPI.Models.ValuatorPro
{
    public class Company
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ValuationTask> ValuationTasks { get; set; } = new();
    }
}
