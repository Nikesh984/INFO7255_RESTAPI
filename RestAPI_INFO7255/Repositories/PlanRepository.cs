using System.Text.Json;
using RestAPI_INFO7255.Models;
using StackExchange.Redis;

namespace RestAPI_INFO7255.Repositories
{
    public class PlanRepository : IPlanRepository
    {

        private readonly IDatabase _db;
        private readonly ILogger<IPlanRepository> _logger;

        public PlanRepository(ILogger<IPlanRepository> logger)
        {
            _logger = logger;
            ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost:6379");
            _db = redis.GetDatabase();
        }

        public async Task CreatePlanAsync(string key, Plan plan)
        {
            var jsonData = JsonSerializer.Serialize(plan);
            await _db.StringSetAsync(key, jsonData);
        }

        public async Task DeletePlanAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        public async Task<Plan?> GetPlanAsync(string key)
        {
            var jsonData = await _db.StringGetAsync(key);
            return jsonData.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Plan>(jsonData);
        }
    }
}