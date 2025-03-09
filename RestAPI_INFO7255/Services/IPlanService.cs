using Microsoft.AspNetCore.JsonPatch;
using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Services
{
    public interface IPlanService
    {
        Task<string> CreatePlan(Plan plan);
        Task<(Plan?, string?)> GetPlanAsync(string planId, string? clientEtag);
        Task DeletePlanAsync(string planId);

        Task<string> UpdatePlanAsync(string planId, Plan updatePlan, string? clientEtag); // Full update
        Task<string> MergePlanAsync(string planId, Plan patchPlan, string? clientEtag); // Updated signature
    }
}