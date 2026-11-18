namespace Cms.Domain.Exceptions;

public class ForbiddenException : Exception
{
    public ForbiddenException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
