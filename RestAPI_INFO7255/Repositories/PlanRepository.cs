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

        public async Task<string> CreatePlanAsync(Plan plan)
        {
            string planKey = $"plan:{plan.ObjectId}";

            // Serialize the plan to JSON
            string planJson = JsonSerializer.Serialize(plan);

            // Generate ETag using SHA256
            string etag = GenerateETag(planKey);

            // Store the plan in Redis with the ETag
            var planWithEtag = new
            {
                Data = plan,
                ETag = etag
            };

            _logger.LogInformation($"Storing plan with key: {planKey}");
            await _db.StringSetAsync(planKey, JsonSerializer.Serialize(planWithEtag));


            return etag; // Return the ETag
        }

        public async Task<(Plan?, string?)> GetPlanAsync(string planId)
        {
            string planKey = $"plan:{planId}";
            _logger.LogInformation($"Attempting to retrieve plan with key: {planKey}");

            string? planJson = await _db.StringGetAsync(planKey);

            if (string.IsNullOrEmpty(planJson))
            {
                _logger.LogWarning($"Plan with ID {planId} not found.");
                return (null, null);
            }

            string etag = GenerateETag(planJson);
            Plan? plan = JsonSerializer.Deserialize<Plan>(planJson);

            return (plan, etag);
        }



        private string GenerateETag(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return $"\"{Convert.ToBase64String(hashBytes)}\""; // Base64 encoding for readability
            }
        }

        // public async Task CreatePlanAsync(string key, Plan plan)
        // {
        //     var jsonData = JsonSerializer.Serialize(plan);
        //     await _db.StringSetAsync(key, jsonData);
        // }


        //working

        // private string GenerateETag(string jsonData)
        // {
        //     using (var sha256 = SHA256.Create())
        //     {
        //         byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(jsonData));
        //         return Convert.ToBase64String(hashBytes);
        //     }
        // }

        //working

        // public async Task<string> CreatePlanAsync(string key, Plan plan)
        // {
        //     var jsonData = JsonSerializer.Serialize(plan);
        //     var etag = GenerateETag(jsonData); // Generate ETag

        //     var planWithEtag = new
        //     {
        //         Data = plan,
        //         ETag = etag
        //     };

        //     await _db.StringSetAsync(key, JsonSerializer.Serialize(planWithEtag));

        //     return etag; // Return the ETag
        // }

        // public string CreatePlan(Plan plan)
        // {
        //     string planKey = $"plan:{plan.ObjectId}";

        //     // Serialize the plan to JSON
        //     string planJson = JsonSerializer.Serialize(plan);

        //     // Compute SHA256 hash for ETag
        //     string etag = GenerateETag(planJson);

        //     // Store the plan in Redis
        //     _db.StringSet(planKey, planJson);

        //     return etag;
        // }

        // private string GenerateETag(string data)
        // {
        //     using (SHA256 sha256 = SHA256.Create())
        //     {
        //         byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
        //         return $"\"{Convert.ToBase64String(hashBytes)}\""; // Base64 encoding for readability
        //     }
        // }




        // public async Task DeletePlanAsync(string key)
        // {
        //     await _db.KeyDeleteAsync(key);
        // }

        // // public async Task<(Plan?, string?)> GetPlanAsync(string key)
        // // {
        // //     var jsonData = await _db.StringGetAsync(key);
        // //     if (jsonData.IsNullOrEmpty) return (null, null);

        // //     var planData = JsonSerializer.Deserialize<JsonElement>(jsonData!);
        // //     var plan = JsonSerializer.Deserialize<Plan>(planData.GetProperty("Data").GetRawText());
        // //     var etag = planData.GetProperty("ETag").GetString();

        // //     return (plan, etag);
        // // }


        // public async Task<(Plan?, string?)> GetPlanAsync(string planId)
        // {
        //     string planKey = $"plan:{planId}";

        //     // Retrieve the JSON data from Redis
        //     string? planJson = await _db.StringGetAsync(planKey);

        //     if (string.IsNullOrEmpty(planJson))
        //     {
        //         return (null, null);
        //     }

        //     // Compute the ETag based on stored data
        //     string etag = GenerateETag(planJson);

        //     // Deserialize the JSON into a Plan object
        //     Plan? plan = JsonSerializer.Deserialize<Plan>(planJson);

        //     return (plan, etag);
        // }


    }
}