using System.Text.Json;
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
    }
}