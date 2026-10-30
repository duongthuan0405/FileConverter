using FileConverterService.Api.ApiResponseModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FileConverterService.Api.Controllers
{
    [ApiController]
    [Route("/check")]
    public class CheckController : ControllerBase
    {
        [HttpGet("health")]
        public Task<ActionResult<ApiSuccessResponse<string>>> CheckHealth()
        {
            ApiSuccessResponse<string> res = new("Service is healthy!", "CHECK HEALTH");
            return Task.FromResult<ActionResult<ApiSuccessResponse<string>>>(Ok(res));
        }

        [HttpGet("ready")]
        public Task<ActionResult<ApiSuccessResponse<string>>> CheckReady()
        {
            ApiSuccessResponse<string> res = new("Service is ready!", "CHECK READY");
            return Task.FromResult<ActionResult<ApiSuccessResponse<string>>>(Ok(res));
        }
    }


}
