using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public class ErrorController : ControllerBase
{
    [Route("/error")]
    public IActionResult HandleError()
    {
        var feature = HttpContext.Features.Get<IExceptionHandlerFeature>();
        var ex = feature?.Error;

        // 👉 log nội bộ nếu muốn
        // _logger.LogError(ex, "Unhandled exception");

        return StatusCode(500, new
        {
            code = 500,
            message = "Hệ thống đang xử lý dữ liệu lớn hoặc bị giới hạn tần suất. Vui lòng thử lại sau."
        });
    }
}
