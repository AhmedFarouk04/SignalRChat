using System.Net;

namespace EnterpriseChat.API.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception");

            var isNotAuthenticated =
                ex is UnauthorizedAccessException &&
                ex.Message.Contains("not authenticated", StringComparison.OrdinalIgnoreCase);

            var (statusCode, title) = ex switch
            {
                UnauthorizedAccessException when isNotAuthenticated
                    => ((int)HttpStatusCode.Unauthorized, "Unauthorized"),

                UnauthorizedAccessException
                    => ((int)HttpStatusCode.Forbidden, "Forbidden"),

                KeyNotFoundException
                    => ((int)HttpStatusCode.NotFound, "Not Found"),

                ArgumentException or InvalidOperationException
                    => ((int)HttpStatusCode.BadRequest, "Bad Request"),

                _ => ((int)HttpStatusCode.InternalServerError, "Internal Server Error")
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsJsonAsync(new
            {
                title,
                status = statusCode,
                error = ex.Message
            });
        }

    }
}
