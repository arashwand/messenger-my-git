using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Messenger.API.Controllers
{
    [ApiController]
    public class ErrorController : ControllerBase
    {
        private readonly ILogger<ErrorController> _logger;

        public ErrorController(ILogger<ErrorController> logger)
        {
            _logger = logger;
        }

        //  UseExceptionHandler
        [Route("/Error")]
        [ApiExplorerSettings(IgnoreApi = true)] // این خط باعث می‌شود این Endpoint در Swagger نمایش داده نشود.
        public IActionResult HandleErrorInProduction()
        {
            // جزئیات خطای رخ داده را از context می‌گیریم.
            var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();
            var exception = exceptionHandlerFeature?.Error;

            // در اینجا می‌توانید خطا را لاگ کنید.
            _logger.LogError(exception, "An unhandled exception has occurred.");

            // یک پاسخ استاندارد و عمومی برای کلاینت‌های API برمی‌گردانیم.
            // هرگز جزئیات خطا را در محیط Production به کلاینت نمی فرستیم
            return Problem(
                title: "An unexpected error occurred on the server.",
                detail: "We are working to resolve the issue. Please try again later.",
                statusCode: StatusCodes.Status500InternalServerError
            );
        }

        // این اکشن برای حالت توسعه استفاده می‌شود تا بتوانیم جزئیات خطا را ببینیم
        // مسیر آن با مسیر بالا متفاوت است.
        [Route("/Error-Development")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult HandleErrorInDevelopment([FromServices] IHostEnvironment hostEnvironment)
        {
            if (!hostEnvironment.IsDevelopment())
            {
                return NotFound();
            }

            var exceptionHandlerFeature = HttpContext.Features.Get<IExceptionHandlerFeature>();

            return Problem(
                detail: exceptionHandlerFeature.Error.StackTrace,
                title: exceptionHandlerFeature.Error.Message
            );
        }
    }
}
