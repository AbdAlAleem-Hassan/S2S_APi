using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace S2S.Web.Middleware
{
    /// <summary>
    /// Catches unhandled exceptions across the entire pipeline and returns
    /// a consistent ProblemDetails (RFC 7807) response without leaking stack traces.
    /// </summary>
    public sealed class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
                var traceId = Guid.NewGuid().ToString("N");

                _logger.LogError(ex, "Unhandled exception. TraceId: {TraceId}, Path: {Path}, Method: {Method}",
                    traceId,
                    context.Request.Path,
                    context.Request.Method);

                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "An internal server error has occurred. Please try again later.",
                    Instance = context.Request.Path,
                    Extensions = { ["traceId"] = traceId }
                };

                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}
