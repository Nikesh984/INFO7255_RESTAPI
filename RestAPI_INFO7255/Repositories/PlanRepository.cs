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
            string planKey = plan.ObjectId;

            // Serialize the plan to JSON
            string planJson = JsonSerializer.Serialize(plan);

            // Generate ETag using SHA256
            string etag = GenerateETag(planJson);

            // Store the plan in Redis without ETag (no need to store the ETag in the database)
            await _db.StringSetAsync(planKey, planJson);

            // Return the generated ETag (this will be returned in the response header)
            return etag;
        }

        public async Task DeletePlanAsync(string planId)
        {
            bool isDeleted = await _db.KeyDeleteAsync(planId);

            if (!isDeleted)
            {
                _logger.LogWarning($"Plan with ID {planId} not found in Redis.");
            }
            else
            {
                _logger.LogInformation($"Plan with ID {planId} deleted successfully.");
            }
        }

        public async Task<(Plan?, string?)> GetPlanAsync(string planId, string? clientEtag)
        {
            string planKey = planId;
            _logger.LogInformation($"Attempting to retrieve plan with key: {planKey}");

            string? planJson = await _db.StringGetAsync(planKey);

            if (string.IsNullOrEmpty(planJson))
            {
                _logger.LogWarning($"Plan with ID {planId} not found.");
                return (null, null);  // Plan not found
            }

            // Generate the ETag based on the plan's current data
            string currentEtag = GenerateETag(planJson);
            _logger.LogInformation($"CurrentEtag = {currentEtag}, ClientETag = {clientEtag}");

            // If the client ETag matches the current ETag, return 304 (Not Modified)
            if (clientEtag != null && clientEtag == currentEtag)
            {
                _logger.LogInformation($"Plan with ETag {clientEtag} not modified.");
                return (null, null);  // Return null indicating 304 Not Modified
            }

            // Deserialize the plan if it's modified
            Plan? plan = JsonSerializer.Deserialize<Plan>(planJson);

            _logger.LogInformation($"Returning plan with ETag: {currentEtag}");
            return (plan, currentEtag);  // Return the plan along with the current ETag
        }

        private string GenerateETag(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return $"\"{Convert.ToBase64String(hashBytes)}\""; // Base64 encoding for readability
            }
        }
    }
}