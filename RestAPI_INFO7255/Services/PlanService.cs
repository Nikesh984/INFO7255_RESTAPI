using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using RestAPI_INFO7255.Models;
using RestAPI_INFO7255.Repositories;

namespace RestAPI_INFO7255.Services
{
    public class PlanService : IPlanService
    {
        private readonly IPlanRepository _planRepository;
        private readonly ILogger<PlanService> _logger;

        public PlanService(ILogger<PlanService> logger, IPlanRepository planRepository)
        {
            _planRepository = planRepository;
            _logger = logger;
        }

        public async Task<string> CreatePlan(Plan plan)
        {
            if (plan != null)
            {
                string etag = await _planRepository.CreatePlanAsync(plan);
                return etag;
            }
            return null;
        }

        public async Task DeletePlanAsync(string planId)
        {
            _logger.LogInformation($"Deleting plan with ID: {planId}");
            await _planRepository.DeletePlanAsync(planId);
        }

        public async Task<(Plan?, string?)> GetPlanAsync(string planId, string? clientEtag)
        {
            return await _planRepository.GetPlanAsync(planId, clientEtag);
        }

        public async Task<string> UpdatePlanAsync(string planId, Plan updatePlan, string? clientEtag)
        {
            if (string.IsNullOrEmpty(planId))
            {
                _logger.LogWarning("UpdatePlan called with null or empty planId.");
                throw new ArgumentNullException(nameof(planId));
            }
            if (updatePlan == null)
            {
                _logger.LogWarning("UpdatePlan called with null updatePlan.");
                throw new ArgumentNullException(nameof(updatePlan));
            }
            if (planId != updatePlan.ObjectId)
            {
                _logger.LogWarning($"PlanId {planId} does not match ObjectId {updatePlan.ObjectId} in updatePlan.");
                throw new ArgumentException("PlanId must match ObjectId in the updatePlan.");
            }
            return await _planRepository.UpdatePlanAsync(planId, updatePlan, clientEtag);
        }

        public async Task<string> MergePlanAsync(string planId, Plan patchPlan, string? clientEtag)
        {
            if (string.IsNullOrEmpty(planId))
            {
                _logger.LogWarning("MergePlan called with null or empty planId.");
                throw new ArgumentNullException(nameof(planId));
            }
            if (patchPlan == null)
            {
                _logger.LogWarning("MergePlan called with null patchPlan.");
                throw new ArgumentNullException(nameof(patchPlan));
            }
            if (planId != patchPlan.ObjectId)
            {
                _logger.LogWarning($"PlanId {planId} does not match ObjectId {patchPlan.ObjectId} in patchPlan.");
                throw new ArgumentException("PlanId must match ObjectId in the patchPlan.");
            }
            return await _planRepository.MergePlanAsync(planId, patchPlan, clientEtag);
        }
    }
}