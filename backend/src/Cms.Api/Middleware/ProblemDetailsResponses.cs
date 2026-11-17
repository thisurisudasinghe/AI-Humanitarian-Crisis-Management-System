using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Middleware;

/// <summary>Shared RFC 7807 writer for middleware-level rejections.</summary>
public static class ProblemDetailsResponses
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static async Task WriteAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string code,
        IReadOnlyDictionary<string, string[]>? errors = null)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path,
            Type = $"https://httpstatuses.io/{statusCode}"
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        if (errors is { Count: > 0 })
        {
            problem.Extensions["errors"] = errors;
        }

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            problem,
            SerializerOptions,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
