using System.Security.Cryptography;
using Cms.Application.Cases.Interfaces;
using Microsoft.AspNetCore.DataProtection;

namespace Cms.Infrastructure.Cases;

public sealed class SensitiveDataProtector : ISensitiveDataProtector
{
    private readonly IDataProtector _protector;

    public SensitiveDataProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Cms.Cases.Sensitive.v1");
    }

    public string? Protect(string? value) =>
        string.IsNullOrEmpty(value) ? value : _protector.Protect(value);

    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        try
        {
            return _protector.Unprotect(value);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public string? MaskPhone(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length <= 2)
        {
            return "**";
        }

        return new string('*', digits.Length - 2) + digits[^2..];
    }

    public string? MaskText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return "***";
    }
}
