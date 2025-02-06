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
                // Generate the ETag and save the plan to Redis
                string etag = await _planRepository.CreatePlanAsync(plan);
                return etag;
            }
            return null;
        }

        public async Task<(Plan?, string?)> GetPlan(string key)
        {
            return await _planRepository.GetPlanAsync(key);
        }


        // public string CreatePlan(Plan plan)
        // {
        //     if (plan != null)
        //     {
        //         string etag = _planRepository.CreatePlan(plan);
        //         return etag;
        //     }

        //     return null;
        // }

        // public async Task DeletePlan(string key)
        // {
        //     await _planRepository.DeletePlanAsync(key);
        // }


        // public async Task<(Plan?, string?)> GetPlan(string key)
        // {
        //     return await _planRepository.GetPlanAsync(key);
        // }

    }
}