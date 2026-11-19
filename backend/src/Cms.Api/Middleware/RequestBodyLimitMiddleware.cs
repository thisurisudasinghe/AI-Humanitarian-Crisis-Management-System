using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Middleware;

/// <summary>Rejects oversized request bodies before they reach controllers.</summary>
public sealed class RequestBodyLimitMiddleware
{
    public const long MaxBodyBytes = 1_048_576;

    private readonly RequestDelegate _next;

    public RequestBodyLimitMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.ContentLength > MaxBodyBytes)
        {
            await ProblemDetailsResponses.WriteAsync(
                context,
                StatusCodes.Status413PayloadTooLarge,
                "Request body too large",
                $"Request bodies larger than {MaxBodyBytes} bytes are rejected.",
                "REQUEST_TOO_LARGE");

            return;
        }

        await _next(context);
    }
}
