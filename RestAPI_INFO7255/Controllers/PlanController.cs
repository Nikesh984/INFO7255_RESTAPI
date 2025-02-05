using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
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

        // Create a new plan
        // [HttpPost]
        // public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        // {
        //     if (!ModelState.IsValid)
        //     {
        //         return BadRequest(ModelState);
        //     }

        //     await _planService.CreatePlan(plan.ObjectId, plan);
        //     return CreatedAtAction(nameof(GetPlan), new { id = plan.ObjectId }, plan);
        // }

        [HttpPost]
        public async Task<IActionResult> CreatePlan([FromBody] Plan plan)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var etag = await _planService.CreatePlan(plan.ObjectId, plan);

            Response.Headers[HeaderNames.ETag] = etag.ToString(); // Add ETag to response headers
            return CreatedAtAction(nameof(GetPlan), new { id = plan.ObjectId }, plan);
        }

        // Retrieve a plan by ID
        // [HttpGet("{id}")]
        // public async Task<IActionResult> GetPlan(string id)
        // {
        //     var plan = await _planService.GetPlan(id);
        //     if (plan == null) return NotFound();
        //     return Ok(plan);
        // }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPlan(string id)
        {
            var (plan, etag) = await _planService.GetPlan(id);
            if (plan == null) return NotFound();

            Response.Headers[HeaderNames.ETag] = etag; // Add ETag to response headers
            return Ok(plan);
        }

        // Delete a plan by ID
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePlan(string id)
        {
            await _planService.DeletePlan(id);
            return NoContent();
        }
    }
}