using Microsoft.AspNetCore.Mvc;

namespace RestAPI_INFO7255.Controllers
{
    [ApiController]
    [Route("api/test")]
    public class TestController : ControllerBase
    {

        [HttpGet]
        public IActionResult getAll()
        {
            return Ok();
        }
    }
}