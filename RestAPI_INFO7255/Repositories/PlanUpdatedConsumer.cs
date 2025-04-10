using MassTransit;
using Nest;
using RestAPI_INFO7255.Models;
using System.Text.Json;

namespace RestAPI_INFO7255.Repositories
{
    public class PlanUpdatedConsumer : IConsumer<PlanUpdatedEvent>
    {
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<PlanUpdatedConsumer> _logger;

        public PlanUpdatedConsumer(IElasticClient elasticClient, ILogger<PlanUpdatedConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PlanUpdatedEvent> context)
        {
            var message = context.Message;
            var plan = JsonSerializer.Deserialize<Plan>(message.PlanJson);
            _logger.LogInformation($"Processing update for Plan {message.PlanId} with ETag {message.ETag}");

            try
            {
                // Index the Plan
                _logger.LogInformation($"Attempting to index Plan {plan.ObjectId} via consumer");
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
                    .Id(plan.ObjectId));
                //.Type("_doc"));
                if (!indexResponse.IsValid)
                {
                    var errorMessage = indexResponse.ServerError?.Error?.Reason ?? indexResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to index Plan {plan.ObjectId} via consumer: {errorMessage}");
                    return;
                }
                _logger.LogInformation($"Plan {plan.ObjectId} indexed successfully via consumer");

                // Index planCostShare
                if (plan.PlanCostShares != null)
                {
                    var costShareDoc = new
                    {
                        plan.PlanCostShares.Deductible,
                        plan.PlanCostShares._org,
                        plan.PlanCostShares.Copay,
                        plan.PlanCostShares.ObjectId,
                        ObjectType = plan.PlanCostShares.ObjectType,
                        joinField = new { name = "planCostShare", parent = plan.ObjectId }
                    };
                    var costShareResponse = await _elasticClient.IndexAsync(costShareDoc, i => i
                        .Index("plans")
                        .Id(plan.PlanCostShares.ObjectId)
                        .Routing(plan.ObjectId));
                    //.Type("_doc"));
                    if (!costShareResponse.IsValid)
                    {
                        var errorMessage = costShareResponse.ServerError?.Error?.Reason ?? costShareResponse.DebugInformation ?? "Unknown error";
                        _logger.LogError($"Failed to index PlanCostShare {plan.PlanCostShares.ObjectId} via consumer: {errorMessage}");
                    }
                    else
                    {
                        _logger.LogInformation($"PlanCostShare {plan.PlanCostShares.ObjectId} indexed successfully via consumer");
                    }
                }

                // Index LinkedPlanServices and their nested objects
                if (plan?.LinkedPlanServices != null)
                {
                    foreach (var service in plan.LinkedPlanServices)
                    {
                        var serviceDoc = new
                        {
                            service._org,
                            service.ObjectId,
                            ObjectType = service.ObjectType,
                            joinField = new { name = "linkedPlanServices", parent = plan.ObjectId }
                        };
                        var serviceResponse = await _elasticClient.IndexAsync(serviceDoc, i => i
                            .Index("plans")
                            .Id(service.ObjectId)
                            .Routing(plan.ObjectId));
                        //.Type("_doc"));
                        if (!serviceResponse.IsValid)
                        {
                            var errorMessage = serviceResponse.ServerError?.Error?.Reason ?? serviceResponse.DebugInformation ?? "Unknown error";
                            _logger.LogError($"Failed to index LinkedPlanService {service.ObjectId} via consumer: {errorMessage}");
                        }
                        else
                        {
                            _logger.LogInformation($"LinkedPlanService {service.ObjectId} indexed successfully via consumer");
                        }

                        if (service.LinkedService != null)
                        {
                            var linkedServiceDoc = new
                            {
                                service.LinkedService._org,
                                service.LinkedService.ObjectId,
                                ObjectType = service.LinkedService.ObjectType,
                                Name = service.LinkedService.Name,
                                joinField = new { name = "linkedService", parent = service.ObjectId }
                            };
                            var linkedServiceResponse = await _elasticClient.IndexAsync(linkedServiceDoc, i => i
                                .Index("plans")
                                .Id(service.LinkedService.ObjectId)
                                .Routing(plan.ObjectId));
                            //.Type("_doc"));
                            if (!linkedServiceResponse.IsValid)
                            {
                                var errorMessage = linkedServiceResponse.ServerError?.Error?.Reason ?? linkedServiceResponse.DebugInformation ?? "Unknown error";
                                _logger.LogError($"Failed to index LinkedService {service.LinkedService.ObjectId} via consumer: {errorMessage}");
                            }
                            else
                            {
                                _logger.LogInformation($"LinkedService {service.LinkedService.ObjectId} indexed successfully via consumer");
                            }
                        }

                        if (service.PlanServiceCostShares != null)
                        {
                            var serviceCostShareDoc = new
                            {
                                service.PlanServiceCostShares.Deductible,
                                service.PlanServiceCostShares._org,
                                service.PlanServiceCostShares.Copay,
                                service.PlanServiceCostShares.ObjectId,
                                ObjectType = service.PlanServiceCostShares.ObjectType,
                                joinField = new { name = "planserviceCostShares", parent = service.ObjectId }
                            };
                            var serviceCostShareResponse = await _elasticClient.IndexAsync(serviceCostShareDoc, i => i
                                .Index("plans")
                                .Id(service.PlanServiceCostShares.ObjectId)
                                .Routing(plan.ObjectId));
                            //.Type("_doc"));
                            if (!serviceCostShareResponse.IsValid)
                            {
                                var errorMessage = serviceCostShareResponse.ServerError?.Error?.Reason ?? serviceCostShareResponse.DebugInformation ?? "Unknown error";
                                _logger.LogError($"Failed to index PlanServiceCostShares {service.PlanServiceCostShares.ObjectId} via consumer: {errorMessage}");
                            }
                            else
                            {
                                _logger.LogInformation($"PlanServiceCostShares {service.PlanServiceCostShares.ObjectId} indexed successfully via consumer");
                            }
                        }
                    }
                }
                _logger.LogInformation($"Plan {plan.ObjectId} and its related objects indexed successfully in Elasticsearch via consumer");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while indexing Plan {plan.ObjectId} via consumer: {ex.Message}");
            }
        }
    }

    // public class PlanUpdatedEvent
    // {
    //     public string PlanId { get; set; }
    //     public string PlanJson { get; set; }
    //     public string ETag { get; set; }
    // }
}