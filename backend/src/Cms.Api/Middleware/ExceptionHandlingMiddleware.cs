using System.Text.Json;
using Cms.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Middleware;

/// <summary>
/// Converts every unhandled exception into an RFC 7807 ProblemDetails response
/// with a stable machine-readable <c>code</c> extension.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

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
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
            {
                _logger.LogError(
                    exception,
                    "An exception occurred after the response started. TraceId: {TraceId}",
                    context.TraceIdentifier);
                throw;
            }

            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title, detail, code, errors) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(
                exception,
                "Unhandled exception. TraceId: {TraceId}",
                context.TraceIdentifier);
        }
        else
        {
            _logger.LogWarning(
                "Request rejected with {StatusCode} ({Code}). TraceId: {TraceId}",
                statusCode,
                code,
                context.TraceIdentifier);
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

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(
            problem,
            SerializerOptions,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private static (int StatusCode, string Title, string Detail, string Code,
        IReadOnlyDictionary<string, string[]>? Errors) Map(Exception exception) =>
        exception switch
        {
            ValidationException validation => (
                StatusCodes.Status422UnprocessableEntity,
                "Validation failed",
                validation.Message,
                validation.Code,
                validation.Errors),

            DomainException domain => (
                StatusCodes.Status400BadRequest,
                "Request rejected",
                domain.Message,
                domain.Code,
                null),

            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Resource not found",
                notFound.Message,
                notFound.Code,
                null),

            ForbiddenException forbidden => (
                StatusCodes.Status403Forbidden,
                "Forbidden",
                forbidden.Message,
                forbidden.Code,
                null),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Conflict",
                conflict.Message,
                conflict.Code,
                null),

            UnauthorizedAccessException => (
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Authentication is required.",
                "UNAUTHORIZED",
                null),

            BadHttpRequestException badRequest => (
                StatusCodes.Status400BadRequest,
                "Bad request",
                badRequest.Message,
                "BAD_REQUEST",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Unexpected error",
                "An unexpected error occurred. Quote the traceId when reporting it.",
                "INTERNAL_ERROR",
                null)
        };
}
