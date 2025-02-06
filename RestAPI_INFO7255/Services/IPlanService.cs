using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Services
{
    public interface IPlanService
    {
        // string CreatePlan(Plan plan);
        // Task<(Plan?, string?)> GetPlan(string key);
        // Task DeletePlan(string key);

        Task<string> CreatePlan(Plan plan);
        Task<(Plan?, string?)> GetPlan(string key);
    }
}