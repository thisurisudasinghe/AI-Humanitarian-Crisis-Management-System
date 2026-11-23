namespace Cms.Application.DTOs.Authentication;

public sealed class ForgotPasswordResponseDto
{
    public string Message { get; set; } = string.Empty;

    public bool EmailDeliveryConfigured { get; set; }
}
