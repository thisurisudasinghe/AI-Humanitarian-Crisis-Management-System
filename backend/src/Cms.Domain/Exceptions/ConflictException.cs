namespace Cms.Domain.Exceptions;

public class ConflictException : Exception
{
    public ConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
