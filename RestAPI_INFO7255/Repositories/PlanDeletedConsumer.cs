// Works with Nest --version 7
using MassTransit;
using Nest;
using RestAPI_INFO7255.Repositories;
using System.Text.Json;

namespace RestAPI_INFO7255.Consumers
{
    public class PlanDeletedConsumer : IConsumer<PlanDeletedEvent>
    {
        private readonly IElasticClient _elasticClient;
        private readonly ILogger<PlanDeletedConsumer> _logger;

        public PlanDeletedConsumer(IElasticClient elasticClient, ILogger<PlanDeletedConsumer> logger)
        {
            _elasticClient = elasticClient;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<PlanDeletedEvent> context)
        {
            var message = context.Message;
            var planId = message.PlanId;
            _logger.LogInformation($"Processing deletion for Plan {planId} via consumer.");

            try
            {
                // Step 1: Refresh the index to ensure documents are searchable
                _logger.LogInformation($"Refreshing plans index in consumer for Plan {planId}...");
                var refreshResponse = await _elasticClient.Indices.RefreshAsync("plans");
                if (!refreshResponse.IsValid)
                {
                    var errorMessage = refreshResponse.ServerError?.Error?.Reason ?? refreshResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to refresh plans index in consumer: {errorMessage}");
                    return;
                }
                _logger.LogInformation($"Plans index refreshed in consumer.");

                // Step 2: Delete any remaining documents related to the Plan
                var deleteByQueryResponse = await _elasticClient.DeleteByQueryAsync<object>(d => d
                    .Index("plans")
                    .Routing(planId)
                    .Refresh(true)
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                // Direct children (planCostShare, linkedPlanServices)
                                s => s
                                    .HasParent<object>(hp => hp
                                        .ParentType("plan")
                                        .Query(pq => pq
                                            .Term(t => t.Field("_id").Value(planId))
                                        )
                                    ),
                                // Grandchildren (linkedService, planserviceCostShares)
                                s => s
                                    .HasParent<object>(hp => hp
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
                    _logger.LogError($"Failed to delete remaining documents for Plan {planId} via consumer: {errorMessage}");
                }
                else
                {
                    _logger.LogInformation($"Matched {deleteByQueryResponse.Total} documents, deleted {deleteByQueryResponse.Deleted} documents for Plan {planId} via consumer.");
                }

                // Step 3: Verify no documents remain
                var verifyResponse = await _elasticClient.SearchAsync<object>(s => s
                    .Index("plans")
                    .Routing(planId)
                    .Query(q => q
                        .Bool(b => b
                            .Should(
                                // Plan
                                s => s
                                    .Bool(b1 => b1
                                        .Must(
                                            m => m.Term(t => t.Field("_id").Value(planId)),
                                            m => m.Term(t => t.Field("joinField.name").Value("plan"))
                                        )
                                    ),
                                // Direct children (planCostShare, linkedPlanServices)
                                s => s
                                    .HasParent<object>(hp => hp
                                        .ParentType("plan")
                                        .Query(pq => pq
                                            .Term(t => t.Field("_id").Value(planId))
                                        )
                                    ),
                                // Grandchildren (linkedService, planserviceCostShares)
                                s => s
                                    .HasParent<object>(hp => hp
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

                if (verifyResponse.Hits.Count > 0)
                {
                    _logger.LogWarning($"Found {verifyResponse.Hits.Count} remaining documents for Plan {planId} after deletion via consumer.");
                    foreach (var hit in verifyResponse.Hits)
                    {
                        _logger.LogWarning($"Remaining document: {JsonSerializer.Serialize(hit.Source)}");
                    }
                }
                else
                {
                    _logger.LogInformation($"No remaining documents found for Plan {planId} after deletion via consumer.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while deleting Plan {planId} via consumer: {ex.Message}");
            }
        }
    }
}