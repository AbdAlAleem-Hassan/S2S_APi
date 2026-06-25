using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using S2S.Domain.Entities.Enums;
using S2S.Services;
using S2S.Shared.Helpers;

namespace S2S.Presentation.Filters
{
    public class TranslationQuotaFilter : IAsyncActionFilter
    {
        private readonly UserUsageService _service;

        public TranslationQuotaFilter(UserUsageService service)
        {
            _service = service;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var userId = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                      ?? context.HttpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value;

            if (userId == null)
            {
                context.Result = new UnauthorizedObjectResult(new
                {
                    error = "Unauthorized",
                    message = "User ID not found in token."
                });
                return;
            }

            var tierClaim = context.HttpContext.User.FindFirst("subscription_tier")?.Value;
            var subscriptionTier = ParseTier(tierClaim);

            var canProceed = await _service.TryConsumeAsync(userId, UsageType.Translation, subscriptionTier);
            if (!canProceed)
            {
                var info = await _service.GetUsageAsync(userId, subscriptionTier);
                context.Result = new ObjectResult(new
                {
                    error = "Translation quota exceeded. Please wait until the window resets.",
                    used = info.Used,
                    limit = info.Limit,
                    remaining = 0,
                    resetsAt = info.ResetsAt,
                    tier = info.Tier
                })
                {
                    StatusCode = StatusCodes.Status429TooManyRequests
                };
                return;
            }

            await next();
        }

        private static SubscriptionTier? ParseTier(string? value)
        {
            if (SubscriptionTierExtensions.TryParseTier(value, out var tier))
                return tier;
            return null;
        }
    }
}
