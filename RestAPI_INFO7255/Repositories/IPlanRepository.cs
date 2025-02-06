using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Repositories
{
    public interface IPlanRepository
    {
        // string CreatePlan(Plan plan);
        // Task<(Plan?, string?)> GetPlanAsync(string key);
        // Task DeletePlanAsync(string key);

        Task<string> CreatePlanAsync(Plan plan);

        Task<(Plan?, string?)> GetPlanAsync(string key);
    }
}