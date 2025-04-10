using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.JsonPatch;
using RestAPI_INFO7255.Models;
using StackExchange.Redis;
using Nest;
using MassTransit;

namespace RestAPI_INFO7255.Repositories
{
    public class PlanRepository : IPlanRepository
    {
        private readonly IDatabase _db;
        private readonly ILogger<IPlanRepository> _logger;
        private readonly IElasticClient _elasticClient;
        private readonly IBus _bus;

        public PlanRepository(ILogger<IPlanRepository> logger, IDatabase db, IElasticClient elasticClient, IBus bus)
        {
            _logger = logger;
            _db = db;
            _elasticClient = elasticClient;
            _bus = bus;
        }

        public async Task<string> CreatePlanAsync(Plan plan)
        {
            string planKey = plan.ObjectId;
            string planJson = JsonSerializer.Serialize(plan);
            string etag = GenerateETag(planJson);

            await _db.StringSetAsync(planKey, planJson);
            await IndexPlanAsync(plan); // Direct indexing for demo
            await _bus.Publish(new PlanUpdatedEvent { PlanId = planKey, PlanJson = planJson, ETag = etag }); // Queue for consistency
            _logger.LogInformation($"Plan {planKey} created and indexed.");
            return etag;
        }

        public async Task DeletePlanAsync(string planId)
        {
            try
            {
                // Step 1: Check if the plan exists in Redis
                string? planJson = await _db.StringGetAsync(planId);
                if (string.IsNullOrEmpty(planJson))
                {
                    _logger.LogWarning($"Plan with ID {planId} not found in Redis.");
                    return;
                }

                // Step 2: Delete from Redis
                bool isDeleted = await _db.KeyDeleteAsync(planId);
                if (!isDeleted)
                {
                    _logger.LogWarning($"Failed to delete plan with ID {planId} from Redis.");
                    return;
                }
                _logger.LogInformation($"Plan with ID {planId} deleted from Redis.");

                // Step 3: Delete the Plan and all related documents from Elasticsearch using delete_by_query
                _logger.LogInformation($"Deleting Plan {planId} and all related documents from Elasticsearch...");
                var deleteByQueryResponse = await _elasticClient.DeleteByQueryAsync<object>(d => d
                    .Index("plans")
                    .Routing(planId) // Add routing parameter
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                s => s.Term(t => t.Field("_id").Value(planId)),
                                s => s.HasParent<object>(hp => hp
                                    .ParentType("plan")
                                    .Query(pq => pq
                                        .Term(t => t.Field("_id").Value(planId))
                                    )
                                ),
                                s => s.HasParent<object>(hp => hp
                                    .ParentType("linkedPlanServices")
                                    .Query(pq => pq
                                        .HasParent<object>(hp2 => hp2
                                            .ParentType("plan")
                                            .Query(pq2 => pq2
                                                .Term(t => t.Field("_id").Value(planId))
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                if (!deleteByQueryResponse.IsValid)
                {
                    var errorMessage = deleteByQueryResponse.ServerError?.Error?.Reason ?? deleteByQueryResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to delete Plan {planId} and related documents from Elasticsearch: {errorMessage}");
                }
                else
                {
                    _logger.LogInformation($"Matched {deleteByQueryResponse.Total} documents, deleted {deleteByQueryResponse.Deleted} documents related to Plan {planId} from Elasticsearch.");
                }

                // Step 4: Publish a PlanDeletedEvent for asynchronous processing
                await _bus.Publish(new PlanDeletedEvent { PlanId = planId });
                _logger.LogInformation($"Published PlanDeletedEvent for Plan {planId}.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting Plan {planId}: {ex.Message}");
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
                return (null, null);
            }

            string currentEtag = GenerateETag(planJson);
            if (clientEtag != null && clientEtag == currentEtag)
            {
                _logger.LogInformation($"Plan with ETag {clientEtag} not modified.");
                return (null, currentEtag);
            }

            Plan? plan = JsonSerializer.Deserialize<Plan>(planJson);
            _logger.LogInformation($"Returning plan with ETag: {currentEtag}");
            return (plan, currentEtag);
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
            await IndexPlanAsync(existingPlan); // Direct indexing
            await _bus.Publish(new PlanUpdatedEvent { PlanId = planKey, PlanJson = updatedJson, ETag = newEtag });
            _logger.LogInformation($"Plan {planKey} updated and indexed.");
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
            await IndexPlanAsync(existingPlan); // Direct indexing for demo
            await _bus.Publish(new PlanUpdatedEvent { PlanId = planKey, PlanJson = updatedJson, ETag = newEtag });
            _logger.LogInformation($"Plan {planKey} merged and indexed.");
            return newEtag;
        }

        private void MergePlans(Plan existingPlan, Plan patchPlan)
        {
            if (patchPlan._org != null) existingPlan._org = patchPlan._org;
            if (patchPlan.ObjectId != null) existingPlan.ObjectId = patchPlan.ObjectId;
            if (patchPlan.ObjectType != null) existingPlan.ObjectType = patchPlan.ObjectType;
            if (patchPlan.PlanType != null) existingPlan.PlanType = patchPlan.PlanType;
            if (patchPlan.CreationDate != default) existingPlan.CreationDate = patchPlan.CreationDate;

            if (patchPlan.PlanCostShares != null)
            {
                existingPlan.PlanCostShares ??= new PlanCostShares();
                if (patchPlan.PlanCostShares.Deductible.HasValue) existingPlan.PlanCostShares.Deductible = patchPlan.PlanCostShares.Deductible;
                if (patchPlan.PlanCostShares.Copay.HasValue) existingPlan.PlanCostShares.Copay = patchPlan.PlanCostShares.Copay;
                if (patchPlan.PlanCostShares._org != null) existingPlan.PlanCostShares._org = patchPlan.PlanCostShares._org;
                if (patchPlan.PlanCostShares.ObjectId != null) existingPlan.PlanCostShares.ObjectId = patchPlan.PlanCostShares.ObjectId;
                if (patchPlan.PlanCostShares.ObjectType != null) existingPlan.PlanCostShares.ObjectType = patchPlan.PlanCostShares.ObjectType;
            }

            if (patchPlan.LinkedPlanServices != null && patchPlan.LinkedPlanServices.Count > 0)
            {
                existingPlan.LinkedPlanServices ??= new List<LinkedPlanService>();
                foreach (var patchService in patchPlan.LinkedPlanServices)
                {
                    var existingService = existingPlan.LinkedPlanServices
                        .FirstOrDefault(s => s.ObjectId == patchService.ObjectId);

                    if (existingService != null)
                    {
                        if (patchService._org != null) existingService._org = patchService._org;
                        if (patchService.ObjectType != null) existingService.ObjectType = patchService.ObjectType;

                        if (patchService.LinkedService != null)
                        {
                            existingService.LinkedService ??= new LinkedService();
                            if (patchService.LinkedService._org != null) existingService.LinkedService._org = patchService.LinkedService._org;
                            if (patchService.LinkedService.ObjectId != null) existingService.LinkedService.ObjectId = patchService.LinkedService.ObjectId;
                            if (patchService.LinkedService.ObjectType != null) existingService.LinkedService.ObjectType = patchService.LinkedService.ObjectType;
                            if (patchService.LinkedService.Name != null) existingService.LinkedService.Name = patchService.LinkedService.Name;
                        }

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
                        existingPlan.LinkedPlanServices.Add(patchService);
                    }
                }
            }
        }

        private async Task IndexPlanAsync(Plan plan)
        {
            try
            {
                // Index the Plan
                _logger.LogInformation($"Attempting to index Plan {plan.ObjectId}");
                var planDoc = new
                {
                    plan._org,
                    plan.ObjectId,
                    plan.ObjectType,
                    plan.PlanType,
                    plan.CreationDate,
                    joinField = new { name = "plan" }
                };
                var indexResponse = await _elasticClient.IndexAsync(planDoc, i => i
                    .Index("plans")
                    .Id(plan.ObjectId)
                    .Type("_doc"));
                if (!indexResponse.IsValid)
                {
                    var errorMessage = indexResponse.ServerError?.Error?.Reason ?? indexResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to index Plan {plan.ObjectId}: {errorMessage}");
                    return;
                }
                _logger.LogInformation($"Plan {plan.ObjectId} indexed successfully");

                // Index planCostShare as a child of Plan
                if (plan.PlanCostShares != null)
                {
                    var costShareDoc = new
                    {
                        plan.PlanCostShares.Deductible,
                        plan.PlanCostShares._org,
                        plan.PlanCostShares.Copay,
                        plan.PlanCostShares.ObjectId,
                        ObjectType = plan.PlanCostShares.ObjectType, // Ensure objectType is set
                        joinField = new { name = "planCostShare", parent = plan.ObjectId }
                    };
                    var costShareResponse = await _elasticClient.IndexAsync(costShareDoc, i => i
                        .Index("plans")
                        .Id(plan.PlanCostShares.ObjectId)
                        .Routing(plan.ObjectId)
                        .Type("_doc"));
                    if (!costShareResponse.IsValid)
                    {
                        var errorMessage = costShareResponse.ServerError?.Error?.Reason ?? costShareResponse.DebugInformation ?? "Unknown error";
                        _logger.LogError($"Failed to index PlanCostShare {plan.PlanCostShares.ObjectId}: {errorMessage}");
                    }
                    else
                    {
                        _logger.LogInformation($"PlanCostShare {plan.PlanCostShares.ObjectId} indexed successfully");
                    }
                }

                // Index LinkedPlanServices and their nested objects
                if (plan.LinkedPlanServices != null)
                {
                    foreach (var service in plan.LinkedPlanServices)
                    {
                        // Index LinkedPlanService as a child of Plan
                        var serviceDoc = new
                        {
                            service._org,
                            service.ObjectId,
                            ObjectType = service.ObjectType, // Ensure objectType is set
                            joinField = new { name = "linkedPlanServices", parent = plan.ObjectId }
                        };
                        var serviceResponse = await _elasticClient.IndexAsync(serviceDoc, i => i
                            .Index("plans")
                            .Id(service.ObjectId)
                            .Routing(plan.ObjectId)
                            .Type("_doc"));
                        if (!serviceResponse.IsValid)
                        {
                            var errorMessage = serviceResponse.ServerError?.Error?.Reason ?? serviceResponse.DebugInformation ?? "Unknown error";
                            _logger.LogError($"Failed to index LinkedPlanService {service.ObjectId}: {errorMessage}");
                        }
                        else
                        {
                            _logger.LogInformation($"LinkedPlanService {service.ObjectId} indexed successfully");
                        }

                        // Index LinkedService as a child of LinkedPlanService
                        if (service.LinkedService != null)
                        {
                            var linkedServiceDoc = new
                            {
                                service.LinkedService._org,
                                service.LinkedService.ObjectId,
                                ObjectType = service.LinkedService.ObjectType, // Ensure objectType is set
                                Name = service.LinkedService.Name,
                                joinField = new { name = "linkedService", parent = service.ObjectId }
                            };
                            var linkedServiceResponse = await _elasticClient.IndexAsync(linkedServiceDoc, i => i
                                .Index("plans")
                                .Id(service.LinkedService.ObjectId)
                                .Routing(plan.ObjectId)
                                .Type("_doc"));
                            if (!linkedServiceResponse.IsValid)
                            {
                                var errorMessage = linkedServiceResponse.ServerError?.Error?.Reason ?? linkedServiceResponse.DebugInformation ?? "Unknown error";
                                _logger.LogError($"Failed to index LinkedService {service.LinkedService.ObjectId}: {errorMessage}");
                            }
                            else
                            {
                                _logger.LogInformation($"LinkedService {service.LinkedService.ObjectId} indexed successfully");
                            }
                        }

                        // Index PlanServiceCostShares as a child of LinkedPlanService
                        if (service.PlanServiceCostShares != null)
                        {
                            var serviceCostShareDoc = new
                            {
                                service.PlanServiceCostShares.Deductible,
                                service.PlanServiceCostShares._org,
                                service.PlanServiceCostShares.Copay,
                                service.PlanServiceCostShares.ObjectId,
                                ObjectType = service.PlanServiceCostShares.ObjectType, // Ensure objectType is set
                                joinField = new { name = "planserviceCostShares", parent = service.ObjectId }
                            };
                            var serviceCostShareResponse = await _elasticClient.IndexAsync(serviceCostShareDoc, i => i
                                .Index("plans")
                                .Id(service.PlanServiceCostShares.ObjectId)
                                .Routing(plan.ObjectId)
                                .Type("_doc"));
                            if (!serviceCostShareResponse.IsValid)
                            {
                                var errorMessage = serviceCostShareResponse.ServerError?.Error?.Reason ?? serviceCostShareResponse.DebugInformation ?? "Unknown error";
                                _logger.LogError($"Failed to index PlanServiceCostShares {service.PlanServiceCostShares.ObjectId}: {errorMessage}");
                            }
                            else
                            {
                                _logger.LogInformation($"PlanServiceCostShares {service.PlanServiceCostShares.ObjectId} indexed successfully");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while indexing Plan {plan.ObjectId}: {ex.Message}");
            }
        }

        private string GenerateETag(string data)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(data));
                return $"\"{Convert.ToBase64String(hashBytes)}\"";
            }
        }
    }

    public class PlanUpdatedEvent
    {
        public string PlanId { get; set; }
        public string PlanJson { get; set; }
        public string ETag { get; set; }
    }

    public class PlanDeletedEvent
    {
        public string PlanId { get; set; }
    }
}