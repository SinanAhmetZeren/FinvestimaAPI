namespace FinvestimaAPI.Services.ValuatorPro
{
    public interface IComparableMultiplesJobService
    {
        Task ProcessJobAsync(Guid jobId);
    }
}
