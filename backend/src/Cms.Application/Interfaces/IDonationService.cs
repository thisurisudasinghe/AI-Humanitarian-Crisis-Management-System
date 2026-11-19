using Cms.Application.DTOs;
using Cms.Application.DTOs.Donations;

namespace Cms.Application.Interfaces;

/// <summary>
/// Lean donation management (M8): donors, campaigns, donations, allocations,
/// campaign expenses and the donor transparency report.
/// </summary>
public interface IDonationService
{
    Task<PagedResultDto<DonorDto>> GetDonorsAsync(
        DonorQueryDto query,
        CancellationToken cancellationToken = default);

    Task<DonorDto> GetDonorAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DonorDto> CreateDonorAsync(
        CreateDonorRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DonorDto> UpdateDonorAsync(
        Guid id,
        UpdateDonorRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<CampaignDto>> GetCampaignsAsync(
        CampaignQueryDto query,
        CancellationToken cancellationToken = default);

    Task<CampaignDto> GetCampaignAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CampaignDto> CreateCampaignAsync(
        CreateCampaignRequestDto request,
        CancellationToken cancellationToken = default);

    Task<CampaignDto> UpdateCampaignAsync(
        Guid id,
        UpdateCampaignRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CampaignExpenseDto>> GetCampaignExpensesAsync(
        Guid campaignId,
        CancellationToken cancellationToken = default);

    Task<CampaignExpenseDto> CreateCampaignExpenseAsync(
        Guid campaignId,
        CreateCampaignExpenseRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<DonationDto>> GetDonationsAsync(
        DonationQueryDto query,
        CancellationToken cancellationToken = default);

    Task<DonationDto> GetDonationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DonationDto> CreateDonationAsync(
        CreateDonationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DonationDto> AllocateDonationAsync(
        Guid donationId,
        CreateDonationAllocationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DonationDto> SetDonationFlagAsync(
        Guid donationId,
        FlagRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DonationAllocationDto> SetAllocationFlagAsync(
        Guid allocationId,
        FlagRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DonationReportDto> GetReportAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default);
}
