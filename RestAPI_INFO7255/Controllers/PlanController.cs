using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RestAPI_INFO7255.Helpers;
using RestAPI_INFO7255.Models;
using RestAPI_INFO7255.Services;

namespace RestAPI_INFO7255.Controllers
{
    [ApiController]
    [Route("v1/plan")]
    [Authorize]
    public class PlanController : ControllerBase
    {
        private readonly ILogger _logger;
        private readonly IPlanService _planService;

        public PlanController(ILogger<PlanController> logger, IPlanService planService)
        {
            _logger = logger;
            _planService = planService;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            if (plan == null)
            {
                return BadRequest(new { message = "Plan data is required." });
            }

            var (existingPlan, dummy) = await _planService.GetPlanAsync(plan.ObjectId, null);

            if (existingPlan != null)
            {
                return Conflict(new { message = "A plan with this ID already exists." });
            }

            var etag = await _planService.CreatePlan(plan);

            // Return the created plan with the ETag header
            Response.Headers.Add("ETag", etag);

            return StatusCode(201, new { message = $"Plan created with object id : {plan.ObjectId}" });
            //return CreatedAtAction(nameof(GetPlan), new { id = plan.ObjectId }, plan);
        }


        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlan(string id, [FromHeader(Name = "If-None-Match")] string? clientEtag)
        {
            var (plan, etag) = await _planService.GetPlanAsync(id, clientEtag);

            // If the plan was not modified, return 304 Not Modified
            if (plan == null)
            {
                return clientEtag != null ? StatusCode(304, new { message = "Content not modified" }) : NotFound();
            }

            // Return the plan with the ETag in the response headers
            Response.Headers.Add("ETag", etag!);
            return Ok(plan);
        }


        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(string id)
        {
            _logger.LogInformation($"Received request to delete plan with ID: {id}");

            var (existingPlan, dummyEtag) = await _planService.GetPlanAsync(id, null);

            if (existingPlan == null)
            {
                return NotFound(new { message = "Plan not found." });
            }

            await _planService.DeletePlanAsync(id);

            _logger.LogInformation($"Successfully deleted plan with ID: {id}");
            return NoContent();
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(string id, [FromBody] Plan plan, [FromHeader(Name = "If-Match")] string? clientEtag)
        {
            if (string.IsNullOrEmpty(id) || plan == null)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID or data is required." });
            }
            if (id != plan.ObjectId)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID in URL must match ObjectId in body." });
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(new ValidationProblemDetails(ModelState));
            }

            try
            {
                var etag = await _planService.UpdatePlanAsync(id, plan, clientEtag);
                var (updatedPlan, _) = await _planService.GetPlanAsync(id, null); // Fetch the merged plan
                return this.WithETag(Ok(updatedPlan), etag);
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Plan not found." });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(412, new ProblemDetails { Title = "Precondition Failed", Detail = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update plan with ID {Id}", id);
                return StatusCode(500, new ProblemDetails { Title = "Server Error", Detail = "Failed to update plan." });
            }
        }

        // [HttpPatch("{id}")]
        // public async Task<IActionResult> MergePlan(string id, [FromBody] Plan patchPlan, [FromHeader(Name = "If-Match")] string? clientEtag)
        // {
        //     if (string.IsNullOrEmpty(id) || patchPlan == null)
        //     {
        //         return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID or patch data is required." });
        //     }
        //     if (id != patchPlan.ObjectId)
        //     {
        //         return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID in URL must match ObjectId in body." });
        //     }
        //     if (!ModelState.IsValid)
        //     {
        //         return BadRequest(new ValidationProblemDetails(ModelState));
        //     }

        //     try
        //     {
        //         var (existingPlan, _) = await _planService.GetPlanAsync(id, null);
        //         if (existingPlan == null) return NotFound();

        //         var etag = await _planService.MergePlanAsync(id, patchPlan, clientEtag);
        //         var (updatedPlan, _) = await _planService.GetPlanAsync(id, null);
        //         return this.WithETag(Ok(updatedPlan), etag);
        //     }
        //     catch (InvalidOperationException ex)
        //     {
        //         return StatusCode(412, new ProblemDetails { Title = "Precondition Failed", Detail = ex.Message });
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Failed to merge plan with ID {Id}", id);
        //         return StatusCode(500, new ProblemDetails { Title = "Server Error", Detail = "Failed to merge plan." });
        //     }
        // }

        [HttpPatch("{id}")]
        public async Task<IActionResult> MergePlan(string id, [FromBody] Plan patchPlan, [FromHeader(Name = "If-None-Match")] string? clientEtag)
        {
            if (string.IsNullOrEmpty(id) || patchPlan == null)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID or patch data is required." });
            }
            if (id != patchPlan.ObjectId)
            {
                return BadRequest(new ProblemDetails { Title = "Invalid Request", Detail = "Plan ID in URL must match ObjectId in body." });
            }
            if (!ModelState.IsValid)
            {
                return BadRequest(new ValidationProblemDetails(ModelState));
            }
            if (string.IsNullOrEmpty(clientEtag))
            {
                return StatusCode(428, new ProblemDetails { Title = "Precondition Required", Detail = "If-Match header with ETag is required for PATCH." });
            }

            try
            {
                var (existingPlan, currentEtag) = await _planService.GetPlanAsync(id, null);
                if (existingPlan == null)
                {
                    return NotFound(new ProblemDetails { Title = "Not Found", Detail = "Plan not found." });
                }

                // Compare client-provided ETag with current ETag
                if (clientEtag != currentEtag)
                {
                    return StatusCode(412, new ProblemDetails { Title = "Precondition Failed", Detail = "ETag mismatch. The resource has been modified." });
                }

                var newEtag = await _planService.MergePlanAsync(id, patchPlan, clientEtag);
                var (updatedPlan, _) = await _planService.GetPlanAsync(id, null);
                return this.WithETag(Ok(updatedPlan), newEtag);
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(412, new ProblemDetails { Title = "Precondition Failed", Detail = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to merge plan with ID {Id}", id);
                return StatusCode(500, new ProblemDetails { Title = "Server Error", Detail = "Failed to merge plan." });
            }
        }

    }
}