using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Repositories
{
    public interface IPlanRepository
    {
        Task CreatePlanAsync(string key, Plan plan);
        Task<Plan?> GetPlanAsync(string key);
        Task DeletePlanAsync(string key);
    }
}