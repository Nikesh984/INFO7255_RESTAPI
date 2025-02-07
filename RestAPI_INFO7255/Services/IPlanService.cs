using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Services
{
    public interface IPlanService
    {
        Task<string> CreatePlan(Plan plan);
        Task<(Plan?, string?)> GetPlanAsync(string planId, string? clientEtag);
        Task DeletePlanAsync(string planId);
    }
}