namespace Cms.Application.DTOs.AI;

/// <summary>Payload of the frozen Request.Submitted outbox event.</summary>
public class RequestSubmittedPayloadDto
{
    public Guid RequestId { get; set; }
}
