using Cms.Application.DTOs;
using Cms.Application.DTOs.Admin;

namespace Cms.Application.Interfaces;

public interface IAdminService
{
    Task<PagedResultDto<UserSummaryDto>> GetUsersAsync(
        string? search,
        string? role,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<UserSummaryDto> GetUserAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UserSummaryDto> CreateUserAsync(
        CreateUserRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserSummaryDto> UpdateUserAsync(
        Guid id,
        UpdateUserRequestDto request,
        CancellationToken cancellationToken = default);

    Task<UserSummaryDto> ReplaceUserRolesAsync(
        Guid id,
        ReplaceRolesRequestDto request,
        CancellationToken cancellationToken = default);

    Task<PagedResultDto<OrganisationDto>> GetOrganisationsAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<OrganisationDto> GetOrganisationAsync(Guid id, CancellationToken cancellationToken = default);

    Task<OrganisationDto> CreateOrganisationAsync(
        CreateOrganisationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<OrganisationDto> UpdateOrganisationAsync(
        Guid id,
        UpdateOrganisationRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SystemSettingDto>> GetConfigAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<SystemSettingDto>> UpdateConfigAsync(
        UpdateConfigRequestDto request,
        CancellationToken cancellationToken = default);
}
