namespace Cms.Application.Common;

/// <summary>Business-rule violation. Maps to HTTP 400.</summary>
public class DomainException : Exception
{
    public DomainException(string message, string code = "DOMAIN_ERROR")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Input validation failure. Maps to HTTP 422.</summary>
public class ValidationException : Exception
{
    public ValidationException(string message, IReadOnlyDictionary<string, string[]>? errors = null,
        string code = "VALIDATION_FAILED")
        : base(message)
    {
        Code = code;
        Errors = errors ?? new Dictionary<string, string[]>();
    }

    public string Code { get; }

    public IReadOnlyDictionary<string, string[]> Errors { get; }
}

/// <summary>Missing resource. Maps to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message, string code = "NOT_FOUND")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>Authorisation failure. Maps to HTTP 403.</summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message, string code = "FORBIDDEN")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>State or uniqueness conflict. Maps to HTTP 409.</summary>
public class ConflictException : Exception
{
    public ConflictException(string message, string code = "CONFLICT")
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
