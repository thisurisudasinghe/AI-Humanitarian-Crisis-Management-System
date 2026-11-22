using Cms.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

public static class CasesProblem
{
    public static ObjectResult From(Exception exception)
    {
        var (status, code, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, exception.Code(), "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, exception.Code(), "Forbidden"),
            ConflictException => (StatusCodes.Status409Conflict, exception.Code(), "Conflict"),
            ValidationException validationException =>
                (StatusCodes.Status422UnprocessableEntity, validationException.Code, "Validation failed"),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Internal server error")
        };

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message
        };
        details.Extensions["code"] = code;

        if (exception is ValidationException { Errors: not null } validation)
        {
            details.Extensions["errors"] = validation.Errors;
        }

        return new ObjectResult(details) { StatusCode = status };
    }

    private static string Code(this Exception exception) => exception switch
    {
        NotFoundException notFound => notFound.Code,
        ForbiddenException forbidden => forbidden.Code,
        ConflictException conflict => conflict.Code,
        _ => "ERROR"
    };
}
