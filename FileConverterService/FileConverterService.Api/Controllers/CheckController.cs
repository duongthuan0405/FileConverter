using FileConverterService.Api.ApiResponseModels;
using FileConverterService.Application.BusinessExceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using FileConverterService.Application.Readiness;

namespace FileConverterService.Api.Controllers
{
    [ApiController]
    [Route("/check")]
    public class CheckController(IReadinessService readiness) : ControllerBase
    {
        [HttpGet("health")]
        public Task<ActionResult<ApiSuccessResponse<string>>> CheckHealth()
        {
            ApiSuccessResponse<string> res = new("Service is healthy!", "CHECK HEALTH");
            return Task.FromResult<ActionResult<ApiSuccessResponse<string>>>(Ok(res));
        }

        [HttpGet("ready")]
        public async Task<IActionResult> CheckReady(CancellationToken cancellationToken)
        {
            var result = await readiness.CheckAsync(cancellationToken);
            if (result.Ready)
                return Ok(new ApiSuccessResponse<ReadinessResult>(result, "Service is ready."));

            var errors = result.Checks.Where(check => !check.Value)
                .ToDictionary(check => check.Key, _ => new List<string> { "Dependency is unavailable." });
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiErrorResponse("Service is not ready.", errors));
        }
    }


}
