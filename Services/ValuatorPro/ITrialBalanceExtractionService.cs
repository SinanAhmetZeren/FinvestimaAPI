namespace FinvestimaAPI.Services.ValuatorPro
{
    public interface ITrialBalanceExtractionService
    {
        // Gemini'yi çağırır, dönen mizan yıllarını doğrulayıp (borç≈alacak) ilgili ValuationTask'a
        // ait TrialBalanceYear/TrialBalanceAccount tablolarına yazar ve job'un durumunu günceller.
        // Kendi DB scope'unu kendisi açar — arka planda (Task.Run ile) çağrılmak üzere tasarlandı.
        Task ProcessJobAsync(Guid jobId, Guid valuationTaskId, byte[] fileBytes, string mimeType);
    }
}
