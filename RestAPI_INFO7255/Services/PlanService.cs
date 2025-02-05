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


        public async Task<Plan> CreatePlan(string key, Plan plan)
        {
            if (plan != null)
            {
                await _planRepository.CreatePlanAsync(key, plan);
            }

            return plan;
        }

        public async Task DeletePlan(string key)
        {
            await _planRepository.DeletePlanAsync(key);
        }


        public async Task<(Plan?, string?)> GetPlan(string key)
        {
            return await _planRepository.GetPlanAsync(key);
        }

    }
}