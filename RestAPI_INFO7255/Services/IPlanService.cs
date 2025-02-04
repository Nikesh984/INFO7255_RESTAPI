using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Services
{
    public interface IPlanService
    {
        Plan CreatePlan(string key, Plan plan);
        Task<Plan?> GetPlan(string key);
        void DeletePlan(string key);
    }
}