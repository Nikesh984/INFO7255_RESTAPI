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
                // Step 1: Delete the Plan document
                var deletePlanResponse = await _elasticClient.DeleteAsync(new DeleteRequest("plans", "_doc", planId));
                if (!deletePlanResponse.IsValid)
                {
                    var errorMessage = deletePlanResponse.ServerError?.Error?.Reason ?? deletePlanResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to delete Plan {planId} from Elasticsearch via consumer: {errorMessage}");
                    return;
                }
                _logger.LogInformation($"Plan {planId} deleted from Elasticsearch via consumer.");

                // Step 2: Delete all child documents
                var searchChildrenResponse = await _elasticClient.SearchAsync<object>(s => s
                    .Index("plans")
                    .Query(q => q
                        .HasParent<object>(hp => hp
                            .ParentType("plan")
                            .Query(pq => pq
                                .Term(t => t.Field("_id").Value(planId))
                            )
                        )
                    )
                );

                if (!searchChildrenResponse.IsValid)
                {
                    var errorMessage = searchChildrenResponse.ServerError?.Error?.Reason ?? searchChildrenResponse.DebugInformation ?? "Unknown error";
                    _logger.LogError($"Failed to search for children of Plan {planId} via consumer: {errorMessage}");
                    return;
                }

                foreach (var hit in searchChildrenResponse.Hits)
                {
                    var childId = hit.Id;
                    // Parse hit.Source as JSON to access joinField.name dynamically
                    var sourceJson = JsonSerializer.Serialize(hit.Source);
                    using var sourceDoc = JsonDocument.Parse(sourceJson);
                    var joinFieldName = sourceDoc.RootElement
                        .GetProperty("joinField")
                        .GetProperty("name")
                        .GetString();

                    if (joinFieldName == "linkedPlanServices")
                    {
                        var searchGrandchildrenResponse = await _elasticClient.SearchAsync<object>(s => s
                            .Index("plans")
                            .Query(q => q
                                .HasParent<object>(hp => hp
                                    .ParentType("linkedPlanServices")
                                    .Query(pq => pq
                                        .Term(t => t.Field("_id").Value(childId))
                                    )
                                )
                            )
                        );

                        if (searchGrandchildrenResponse.IsValid)
                        {
                            foreach (var grandChildHit in searchGrandchildrenResponse.Hits)
                            {
                                var grandChildId = grandChildHit.Id;
                                var deleteGrandChildResponse = await _elasticClient.DeleteAsync(new DeleteRequest("plans", "_doc", grandChildId));
                                if (!deleteGrandChildResponse.IsValid)
                                {
                                    var errorMessage = deleteGrandChildResponse.ServerError?.Error?.Reason ?? deleteGrandChildResponse.DebugInformation ?? "Unknown error";
                                    _logger.LogError($"Failed to delete grandchild {grandChildId} of Plan {planId} via consumer: {errorMessage}");
                                }
                                else
                                {
                                    _logger.LogInformation($"Deleted grandchild {grandChildId} of Plan {planId} from Elasticsearch via consumer.");
                                }
                            }
                        }
                    }

                    var deleteChildResponse = await _elasticClient.DeleteAsync(new DeleteRequest("plans", "_doc", childId));
                    if (!deleteChildResponse.IsValid)
                    {
                        var errorMessage = deleteChildResponse.ServerError?.Error?.Reason ?? deleteChildResponse.DebugInformation ?? "Unknown error";
                        _logger.LogError($"Failed to delete child {childId} of Plan {planId} via consumer: {errorMessage}");
                    }
                    else
                    {
                        _logger.LogInformation($"Deleted child {childId} of Plan {planId} from Elasticsearch via consumer.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Exception while deleting Plan {planId} from Elasticsearch via consumer: {ex.Message}");
            }
        }
    }
}


// using MassTransit;
// using Nest;
// using RestAPI_INFO7255.Repositories;

// namespace RestAPI_INFO7255.Consumers
// {
//     public class PlanDeletedConsumer : IConsumer<PlanDeletedEvent>
//     {
//         private readonly IElasticClient _elasticClient;
//         private readonly ILogger<PlanDeletedConsumer> _logger;

//         public PlanDeletedConsumer(IElasticClient elasticClient, ILogger<PlanDeletedConsumer> logger)
//         {
//             _elasticClient = elasticClient;
//             _logger = logger;
//         }

//         public async Task Consume(ConsumeContext<PlanDeletedEvent> context)
//         {
//             var message = context.Message;
//             var planId = message.PlanId;
//             _logger.LogInformation($"Processing deletion for Plan {planId} via consumer.");

//             try
//             {
//                 Delete the Plan and all related documents from Elasticsearch using delete_by_query
//                 var deleteByQueryResponse = await _elasticClient.DeleteByQueryAsync<object>(d => d
//                     .Index("plans")
//                     .Query(q => q
//                         .Bool(b => b
//                             .Should(
//                                 s => s.Term(t => t.Field("_id").Value(planId)),
//                                 s => s.HasParent<object>(hp => hp
//                                     .ParentType("plan")
//                                     .Query(pq => pq
//                                         .Term(t => t.Field("_id").Value(planId))
//                                     )
//                                 ),
//                                 s => s.HasParent<object>(hp => hp
//                                     .ParentType("linkedPlanServices")
//                                     .Query(pq => pq
//                                         .HasParent<object>(hp2 => hp2
//                                             .ParentType("plan")
//                                             .Query(pq2 => pq2
//                                                 .Term(t => t.Field("_id").Value(planId))
//                                             )
//                                         )
//                                     )
//                                 )
//                             )
//                         )
//                     )
//                 );

//                 if (!deleteByQueryResponse.IsValid)
//                 {
//                     var errorMessage = deleteByQueryResponse.ServerError?.Error?.Reason ?? deleteByQueryResponse.DebugInformation ?? "Unknown error";
//                     _logger.LogError($"Failed to delete Plan {planId} and related documents from Elasticsearch via consumer: {errorMessage}");
//                 }
//                 else
//                 {
//                     _logger.LogInformation($"Deleted {deleteByQueryResponse.Deleted} documents related to Plan {planId} from Elasticsearch via consumer.");
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"Exception while deleting Plan {planId} from Elasticsearch via consumer: {ex.Message}");
//             }
//         }
//     }
// }

// using MassTransit;
// using Nest;
// using RestAPI_INFO7255.Repositories;

// namespace RestAPI_INFO7255.Consumers
// {
//     public class PlanDeletedConsumer : IConsumer<PlanDeletedEvent>
//     {
//         private readonly IElasticClient _elasticClient;
//         private readonly ILogger<PlanDeletedConsumer> _logger;

//         public PlanDeletedConsumer(IElasticClient elasticClient, ILogger<PlanDeletedConsumer> logger)
//         {
//             _elasticClient = elasticClient;
//             _logger = logger;
//         }

//         public async Task Consume(ConsumeContext<PlanDeletedEvent> context)
//         {
//             var message = context.Message;
//             var planId = message.PlanId;
//             _logger.LogInformation($"Processing deletion for Plan {planId} via consumer.");

//             try
//             {
//                 // Step 1: Add a delay to ensure documents are searchable (temporary workaround)
//                 _logger.LogInformation($"Waiting 2 seconds to ensure documents are searchable in consumer for Plan {planId}...");
//                 await Task.Delay(2000); // Wait 2 seconds
//                 _logger.LogInformation($"Finished waiting in consumer for Plan {planId}.");

//                 // Step 2: Check if the plan still exists
//                 var searchResponse = await _elasticClient.SearchAsync<object>(s => s
//                     .Index("plans")
//                     .Query(q => q
//                         .Bool(b => b
//                             .Should(
//                                 s => s.Term(t => t.Field("_id").Value(planId)),
//                                 s => s.Term(t => t.Field("joinField.parent").Value(planId))
//                             )
//                         )
//                     )
//                 );

//                 if (!searchResponse.IsValid)
//                 {
//                     var errorMessage = searchResponse.ServerError?.Error?.Reason ?? searchResponse.DebugInformation ?? "Unknown error";
//                     _logger.LogError($"Failed to search for remaining documents of Plan {planId} in consumer: {errorMessage}");
//                     return;
//                 }

//                 if (searchResponse.Total > 0)
//                 {
//                     _logger.LogWarning($"Found {searchResponse.Total} documents still present for Plan {planId} after deletion. Attempting to delete them...");

//                     // Step 3: Delete any remaining documents
//                     var deleteByQueryResponse = await _elasticClient.DeleteByQueryAsync<object>(d => d
//                         .Index("plans")
//                         .Routing(planId)
//                         .Query(q => q
//                             .Bool(b => b
//                                 .Should(
//                                     s => s.Term(t => t.Field("_id").Value(planId)),
//                                     s => s.HasParent<object>(hp => hp
//                                         .ParentType("plan")
//                                         .Query(pq => pq
//                                             .Term(t => t.Field("_id").Value(planId))
//                                         )
//                                     ),
//                                     s => s.HasParent<object>(hp => hp
//                                         .ParentType("linkedPlanServices")
//                                         .Query(pq => pq
//                                             .HasParent<object>(hp2 => hp2
//                                                 .ParentType("plan")
//                                                 .Query(pq2 => pq2
//                                                     .Term(t => t.Field("_id").Value(planId))
//                                                 )
//                                             )
//                                         )
//                                     )
//                                 )
//                             )
//                         )
//                     );

//                     if (!deleteByQueryResponse.IsValid)
//                     {
//                         var errorMessage = deleteByQueryResponse.ServerError?.Error?.Reason ?? deleteByQueryResponse.DebugInformation ?? "Unknown error";
//                         _logger.LogError($"Failed to delete remaining documents for Plan {planId} in consumer: {errorMessage}");
//                     }
//                     else
//                     {
//                         _logger.LogInformation($"Matched {deleteByQueryResponse.Total} documents, deleted {deleteByQueryResponse.Deleted} documents for Plan {planId} in consumer.");
//                     }
//                 }
//                 else
//                 {
//                     _logger.LogInformation($"No remaining documents found for Plan {planId} after deletion.");
//                 }
//             }
//             catch (Exception ex)
//             {
//                 _logger.LogError($"Exception while processing PlanDeletedEvent for Plan {planId} in consumer: {ex.Message}");
//             }
//         }
//     }
// }