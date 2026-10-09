namespace FinvestimaAPI.Services.ValuatorPro
{
    public interface IMizanExtractionService
    {
        // Gemini'yi çağırır, dönen mizan yıllarını doğrulayıp (borç≈alacak) ilgili
        // Firma'ya ait MizanYil/MizanHesap tablolarına yazar ve job'un durumunu günceller.
        // Kendi DB scope'unu kendisi açar — arka planda (Task.Run ile) çağrılmak üzere tasarlandı.
        Task ProcessJobAsync(Guid jobId, Guid firmaId, byte[] fileBytes, string mimeType);
    }
}
