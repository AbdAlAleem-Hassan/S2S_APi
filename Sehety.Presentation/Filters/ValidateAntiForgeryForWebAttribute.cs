using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace S2S.Presentation.Filters
{
    /// <summary>
    /// Validates anti-forgery tokens only for web (cookie-based) clients.
    /// Mobile clients using body-based refresh tokens are exempt
    /// since CSRF requires browser auto-attached cookies to be exploitable.
    /// Skipped in Development environment to allow Swagger UI testing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public sealed class ValidateAntiForgeryForWebAttribute : ActionFilterAttribute
    {
        public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var httpContext = context.HttpContext;

            // Skip CSRF validation in Development to allow Swagger UI testing.
            // Swagger doesn't send X-XSRF-TOKEN header automatically.
            var env = httpContext.RequestServices.GetService<IHostEnvironment>();
            if (env?.IsDevelopment() == true)
            {
                await next();
                return;
            }

            // Only validate when the client uses cookie-based auth (web flow).
            // Mobile clients send refresh tokens in the request body and are
            // not vulnerable to CSRF since there's no browser auto-attaching cookies.
            if (httpContext.Request.Cookies.ContainsKey("refreshToken"))
            {
                var antiforgery = httpContext.RequestServices.GetRequiredService<IAntiforgery>();

                try
                {
                    await antiforgery.ValidateRequestAsync(httpContext);
                }
                catch (AntiforgeryValidationException ex)
                {
                    context.Result = new BadRequestObjectResult(new
                    {
                        error = "anti-forgery validation failed",
                        detail = ex.Message
                    });
                    return;
                }
            }

            await next();
        }
    }
}
