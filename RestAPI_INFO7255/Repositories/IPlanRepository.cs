using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Repositories
{
    public interface IPlanRepository
    {
        Task<string> CreatePlanAsync(string key, Plan plan);
        Task<(Plan?, string?)> GetPlanAsync(string key);
        Task DeletePlanAsync(string key);
    }
}