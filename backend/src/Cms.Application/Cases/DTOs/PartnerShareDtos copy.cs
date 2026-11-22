using System.ComponentModel.DataAnnotations;
using Cms.Domain.Enums.Cases;

namespace Cms.Application.Cases.DTOs;

/// <summary>
/// Case staff share a request with a partner organisation. Revocation uses the
/// share id via DELETE; there is no partner accept/reject flow in this release.
/// </summary>
public sealed class CreatePartnerShareDto
{
    [Required]
    public Guid RequestId { get; set; }

    [Required]
    public Guid OrganisationId { get; set; }
}

/// <summary>
/// Active share visible to the partner organisation. Request status/category/
/// urgency are read through IRequestReader; no request description or sensitive
/// household field is duplicated here.
/// </summary>
public sealed class PartnerShareDto
{
    public Guid Id { get; set; }

    public Guid RequestId { get; set; }

    public Guid OrganisationId { get; set; }

    public Guid SharedByUserId { get; set; }

    public DateTime SharedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public RequestStatus? Status { get; set; }

    public Category? Category { get; set; }

    public Urgency? Urgency { get; set; }
}
