using System.Net;
using EnterpriseChat.API.Auth;

namespace EnterpriseChat.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            // لو الـ response بدأ، ماينفعش نغيّر status/body
            if (context.Response.HasStarted)
            {
                _logger.LogError(ex, "Exception occurred after response started.");
                throw; // الأفضل هنا تسيبه يطلع (أو return لو عايز تتجاهله)
            }

            var isAuthenticated = context.User?.Identity?.IsAuthenticated == true;

            // Logging مناسب (مش كل حاجة Error)
            if (ex is AuthException)
                _logger.LogWarning(ex, "Handled auth exception");
            else if (ex is UnauthorizedAccessException)
                _logger.LogWarning(ex, "Forbidden/Unauthorized access");
            else if (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
                _logger.LogWarning(ex, "Handled business/validation exception");
            else
                _logger.LogError(ex, "Unhandled exception");

            var statusCode = ex switch
            {
                AuthException aex => NormalizeStatusCode(aex.StatusCode),

                // لو مش authenticated فعلاً -> 401
                UnauthorizedAccessException when !isAuthenticated => (int)HttpStatusCode.Unauthorized,
                // authenticated بس مش مسموح -> 403
                UnauthorizedAccessException => (int)HttpStatusCode.Forbidden,

                KeyNotFoundException => (int)HttpStatusCode.NotFound,
                ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest,

                _ => (int)HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = statusCode;

            var userMessage = statusCode == 500
                ? "Something went wrong. Please try again."
                : ex.Message;

            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(userMessage);
        }
    }

    private static int NormalizeStatusCode(int code)
        => code is >= 400 and <= 599 ? code : (int)HttpStatusCode.InternalServerError;
}
