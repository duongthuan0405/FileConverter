using FileConverterService.Api.ApiResponseModels;
using FileConverterService.Application.UseCases.ConvertToPDF;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FileConverterService.Api.Controllers
{
    [Route("/api/conversions")]
    [ApiController]
    public class ConversionsController : ControllerBase
    {
        private readonly ISender _sender;
        public ConversionsController(ISender sender)
        {
            _sender = sender;
        }


        /// <summary>
        /// Tiếp nhận nhiều file cùng lúc. Mỗi file được cấp một jobId riêng và xử lý độc lập.
        /// </summary>
        /// <param name="files">Danh sách file Office (docx, xlsx, pptx...) cần chuyển đổi</param>
        /// <param name="cancellationToken">Token hỗ trợ hủy tác vụ khi client ngắt kết nối</param>
        /// <returns>Trả về 202 Accepted cùng danh sách các jobId tương ứng với từng file</returns>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ApiSuccessResponse<object>>> ConvertToPDF(
            [FromForm] List<IFormFile> files,
            CancellationToken cancellationToken = default)
        {
            ConverToPDF_UCInput inp = new()
            {
                Files = files.Select(f => new FileParams()
                {
                    FileName = f.FileName,
                    Type = f.ContentType,
                    Content = f.OpenReadStream()
                })
            };

            var res = await _sender.Send(inp);
            return StatusCode(StatusCodes.Status202Accepted, res);
        }

        /// <summary>
        /// Tra cứu trạng thái tiến độ xử lý của từng file (thông qua jobId của file đó).
        /// </summary>
        /// <param name="jobId">Mã định danh của Job (đại diện cho 1 file)</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        [HttpGet("job/{jobId}")]
        public Task<ActionResult<ApiSuccessResponse<object>>> GetJob(
            [FromRoute] string jobId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Tải về file kết quả PDF của từng file riêng biệt sau khi chuyển đổi xong.
        /// </summary>
        /// <param name="jobId">Mã định danh của Job (file cần tải)</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        [HttpGet("job/{jobId}/file")]
        public Task<IActionResult> GetFileOfJob(
            [FromRoute] string jobId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Xóa dọn dẹp tài nguyên tạm (file input, file PDF kết quả) của một Job cụ thể trên Gateway.
        /// </summary>
        /// <param name="jobId">Mã định danh của Job cần dọn dẹp</param>
        /// <param name="cancellationToken">Token hủy tác vụ</param>
        [HttpDelete("job/{jobId}")]
        public Task<ActionResult<ApiSuccessResponse<string>>> DeleteResourceRelatedToJob(
            [FromRoute] string jobId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
