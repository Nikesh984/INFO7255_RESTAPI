using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
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

        public async Task<string> UpdatePlanAsync(string planId, Plan updatePlan, string? clientEtag)
        {
            string planKey = planId;
            string? existingJson = await _db.StringGetAsync(planKey);

            if (string.IsNullOrEmpty(existingJson))
            {
                _logger.LogWarning($"Plan with ID {planKey} not found for update.");
                throw new KeyNotFoundException("Plan not found.");
            }

            string currentEtag = GenerateETag(existingJson);
            if (clientEtag != null && clientEtag != currentEtag)
            {
                _logger.LogWarning($"ETag mismatch for plan {planKey}. Client: {clientEtag}, Current: {currentEtag}");
                throw new InvalidOperationException("ETag mismatch. Plan has been modified.");
            }

            Plan? existingPlan = JsonSerializer.Deserialize<Plan>(existingJson);
            MergePlans(existingPlan!, updatePlan);

            string updatedJson = JsonSerializer.Serialize(existingPlan);
            string newEtag = GenerateETag(updatedJson);
            await _db.StringSetAsync(planKey, updatedJson);
            _logger.LogInformation($"Plan {planKey} updated successfully.");
            return newEtag;
        }

        public async Task<string> MergePlanAsync(string planId, Plan patchPlan, string? clientEtag)
        {
            string planKey = planId;
            string? existingJson = await _db.StringGetAsync(planKey);

            if (string.IsNullOrEmpty(existingJson))
            {
                _logger.LogWarning($"Plan with ID {planKey} not found for merge.");
                throw new KeyNotFoundException("Plan not found.");
            }

            string currentEtag = GenerateETag(existingJson);
            if (clientEtag != null && clientEtag != currentEtag)
            {
                _logger.LogWarning($"ETag mismatch for plan {planKey}. Client: {clientEtag}, Current: {currentEtag}");
                throw new InvalidOperationException("ETag mismatch. Plan has been modified.");
            }

            Plan? existingPlan = JsonSerializer.Deserialize<Plan>(existingJson);
            MergePlans(existingPlan!, patchPlan);

            string updatedJson = JsonSerializer.Serialize(existingPlan);
            string newEtag = GenerateETag(updatedJson);
            await _db.StringSetAsync(planKey, updatedJson);
            _logger.LogInformation($"Plan {planKey} merged successfully.");
            return newEtag;
        }

        private void MergePlans(Plan existingPlan, Plan patchPlan)
        {
            // Merge top-level fields
            if (patchPlan._org != null) existingPlan._org = patchPlan._org;
            if (patchPlan.ObjectId != null) existingPlan.ObjectId = patchPlan.ObjectId;
            if (patchPlan.ObjectType != null) existingPlan.ObjectType = patchPlan.ObjectType;
            if (patchPlan.PlanType != null) existingPlan.PlanType = patchPlan.PlanType;
            if (patchPlan.CreationDate != default) existingPlan.CreationDate = patchPlan.CreationDate;

            // Merge PlanCostShares
            if (patchPlan.PlanCostShares != null)
            {
                existingPlan.PlanCostShares ??= new PlanCostShares();
                if (patchPlan.PlanCostShares.Deductible.HasValue) existingPlan.PlanCostShares.Deductible = patchPlan.PlanCostShares.Deductible;
                if (patchPlan.PlanCostShares.Copay.HasValue) existingPlan.PlanCostShares.Copay = patchPlan.PlanCostShares.Copay;
                if (patchPlan.PlanCostShares._org != null) existingPlan.PlanCostShares._org = patchPlan.PlanCostShares._org;
                if (patchPlan.PlanCostShares.ObjectId != null) existingPlan.PlanCostShares.ObjectId = patchPlan.PlanCostShares.ObjectId;
                if (patchPlan.PlanCostShares.ObjectType != null) existingPlan.PlanCostShares.ObjectType = patchPlan.PlanCostShares.ObjectType;
            }

            // Merge LinkedPlanServices
            if (patchPlan.LinkedPlanServices != null && patchPlan.LinkedPlanServices.Count > 0)
            {
                foreach (var patchService in patchPlan.LinkedPlanServices)
                {
                    var existingService = existingPlan.LinkedPlanServices
                        .FirstOrDefault(s => s.ObjectId == patchService.ObjectId);

                    if (existingService != null)
                    {
                        // Update existing service
                        if (patchService._org != null) existingService._org = patchService._org;
                        if (patchService.ObjectType != null) existingService.ObjectType = patchService.ObjectType;

                        // Merge LinkedService
                        if (patchService.LinkedService != null)
                        {
                            existingService.LinkedService ??= new LinkedService();
                            if (patchService.LinkedService._org != null) existingService.LinkedService._org = patchService.LinkedService._org;
                            if (patchService.LinkedService.ObjectId != null) existingService.LinkedService.ObjectId = patchService.LinkedService.ObjectId;
                            if (patchService.LinkedService.ObjectType != null) existingService.LinkedService.ObjectType = patchService.LinkedService.ObjectType;
                            if (patchService.LinkedService.Name != null) existingService.LinkedService.Name = patchService.LinkedService.Name;
                        }

                        // Merge PlanServiceCostShares
                        if (patchService.PlanServiceCostShares != null)
                        {
                            existingService.PlanServiceCostShares ??= new PlanServiceCostShares();
                            if (patchService.PlanServiceCostShares.Deductible.HasValue) existingService.PlanServiceCostShares.Deductible = patchService.PlanServiceCostShares.Deductible;
                            if (patchService.PlanServiceCostShares.Copay.HasValue) existingService.PlanServiceCostShares.Copay = patchService.PlanServiceCostShares.Copay;
                            if (patchService.PlanServiceCostShares._org != null) existingService.PlanServiceCostShares._org = patchService.PlanServiceCostShares._org;
                            if (patchService.PlanServiceCostShares.ObjectId != null) existingService.PlanServiceCostShares.ObjectId = patchService.PlanServiceCostShares.ObjectId;
                            if (patchService.PlanServiceCostShares.ObjectType != null) existingService.PlanServiceCostShares.ObjectType = patchService.PlanServiceCostShares.ObjectType;
                        }
                    }
                    else
                    {
                        // Add new service if it doesn’t exist
                        existingPlan.LinkedPlanServices.Add(patchService);
                    }
                }
            }
        }
    }
}