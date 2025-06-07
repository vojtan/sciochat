using Microsoft.AspNetCore.Mvc;

namespace Scio.ChatBotApi
{
    internal static class ActionResults
    {
        internal static IActionResult GetErrorResult(string text)
        {
            return new OkObjectResult(new
            {
                status = "error",
                text
            });
        }

        internal static IActionResult GetSuccessResult(string text)
        {
            return new OkObjectResult(new
            {
                status = "success",
                text
            });
        }

        internal static IActionResult GetViolationResult(string message = "")
        {
            return new OkObjectResult(new
            {
                status = "usageViolation",
                message
            });
        }

    }
}
