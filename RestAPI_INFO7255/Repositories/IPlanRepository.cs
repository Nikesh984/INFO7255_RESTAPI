using Microsoft.AspNetCore.JsonPatch;
using RestAPI_INFO7255.Models;

namespace RestAPI_INFO7255.Repositories
{
    public interface IPlanRepository
    {
        Task<string> CreatePlanAsync(Plan plan);

        Task<(Plan?, string?)> GetPlanAsync(string planId, string? clientEtag);

        Task DeletePlanAsync(string planId);

        Task<string> UpdatePlanAsync(string planId, Plan updatePlan, string? clientEtag); // Full update (PUT)
        Task<string> MergePlanAsync(string planId, Plan patchPlan, string? clientEtag); // Partial update with merge (PATCH)
    }
}