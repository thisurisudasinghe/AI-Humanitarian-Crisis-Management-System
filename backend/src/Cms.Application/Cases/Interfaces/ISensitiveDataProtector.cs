namespace Cms.Application.Cases.Interfaces;

public interface ISensitiveDataProtector
{
    string? Protect(string? value);
    string? Unprotect(string? value);
    string? MaskPhone(string? value);
    string? MaskText(string? value);
}
