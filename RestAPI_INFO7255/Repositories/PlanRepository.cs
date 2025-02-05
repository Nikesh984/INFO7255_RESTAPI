using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RestAPI_INFO7255.Models;
using StackExchange.Redis;

namespace RestAPI_INFO7255.Repositories
{
    public class PlanRepository : IPlanRepository
    {

        private readonly IDatabase _db;
        private readonly ILogger<IPlanRepository> _logger;

        public PlanRepository(ILogger<IPlanRepository> logger, IDatabase db)
        {
            _logger = logger;
            _db = db;
        }

        // public async Task CreatePlanAsync(string key, Plan plan)
        // {
        //     var jsonData = JsonSerializer.Serialize(plan);
        //     await _db.StringSetAsync(key, jsonData);
        // }

        private string GenerateETag(string jsonData)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonData));
                return Convert.ToBase64String(hashBytes);
            }
        }

        public async Task<string> CreatePlanAsync(string key, Plan plan)
        {
            var jsonData = JsonSerializer.Serialize(plan);
            var etag = GenerateETag(jsonData); // Generate ETag

            var planWithEtag = new
            {
                Data = plan,
                ETag = etag
            };

            await _db.StringSetAsync(key, JsonSerializer.Serialize(planWithEtag));

            return etag; // Return the ETag
        }





        public async Task DeletePlanAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
        }

        // public async Task<Plan?> GetPlanAsync(string key)
        // {
        //     var jsonData = await _db.StringGetAsync(key);
        //     return jsonData.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Plan>(jsonData);
        // }

        public async Task<(Plan?, string?)> GetPlanAsync(string key)
        {
            var jsonData = await _db.StringGetAsync(key);
            if (jsonData.IsNullOrEmpty) return (null, null);

            var planData = JsonSerializer.Deserialize<JsonElement>(jsonData!);
            var plan = JsonSerializer.Deserialize<Plan>(planData.GetProperty("Data").GetRawText());
            var etag = planData.GetProperty("ETag").GetString();

            return (plan, etag);
        }
    }
}