using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cms.Api.Middleware;

/// <summary>
/// Converts ASP.NET model-state failures into the same ProblemDetails shape as
/// the exception middleware, with code VALIDATION_FAILED and HTTP 422.
/// </summary>
public static class ValidationProblemResponse
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => error.ErrorMessage)
                    .Where(message => !string.IsNullOrWhiteSpace(message))
                    .ToArray());

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status422UnprocessableEntity,
            Title = "Validation failed",
            Detail = "One or more fields are invalid.",
            Instance = context.HttpContext.Request.Path,
            Type = "https://httpstatuses.io/422"
        };

        problem.Extensions["code"] = "VALIDATION_FAILED";
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
        problem.Extensions["errors"] = errors;

        return new ObjectResult(problem)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity,
            ContentTypes = { "application/problem+json" }
        };
    }
}
