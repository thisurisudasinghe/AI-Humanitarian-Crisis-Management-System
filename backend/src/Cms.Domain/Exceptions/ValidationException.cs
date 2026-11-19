namespace Cms.Domain.Exceptions;

public class ValidationException : Exception
{
    public ValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public ValidationException(string code, IDictionary<string, string[]> errors)
        : base("Request validation failed.")
    {
        Code = code;
        Errors = errors;
    }

    public string Code { get; }

    public IDictionary<string, string[]>? Errors { get; }
}
