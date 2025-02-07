using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RestAPI_INFO7255.Helpers;
using RestAPI_INFO7255.Models;
using RestAPI_INFO7255.Services;

namespace RestAPI_INFO7255.Controllers
{
    [ApiController]
    [Route("v1/plan")]
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
    }
}