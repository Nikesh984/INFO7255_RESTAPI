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
                return BadRequest("Plan data is required.");
            }

            // Call the service to create the plan and get the ETag
            string etag = await _planService.CreatePlan(plan);

            // Return response with ETag header
            Response.Headers[HeaderNames.ETag] = etag;
            return CreatedAtAction(nameof(GetPlan), new { id = plan.ObjectId }, plan);
        }

        [HttpGet]
        public async Task<IActionResult> GetPlan([FromQuery] string id)
        {
            var (plan, etag) = await _planService.GetPlan(id);
            if (plan == null) return NotFound();

            if (Request.Headers.TryGetValue("If-None-Match", out var requestEtag) && requestEtag == etag)
            {
                return StatusCode(StatusCodes.Status304NotModified); // No changes
            }

            // Add ETag to response header
            Response.Headers[HeaderNames.ETag] = etag;
            return Ok(plan);
        }

    }
}