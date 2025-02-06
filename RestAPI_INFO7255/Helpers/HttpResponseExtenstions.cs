using Microsoft.AspNetCore.Mvc;

namespace RestAPI_INFO7255.Helpers
{
    public static class HttpResponseExtensions
    {
        public static IActionResult WithETag(this ControllerBase controller, IActionResult response, string etag)
        {
            if (!string.IsNullOrEmpty(etag))
            {
                controller.Response.Headers["ETag"] = etag;
            }
            return response;
        }
    }

}


