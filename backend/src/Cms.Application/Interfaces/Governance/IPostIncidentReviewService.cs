using Cms.Application.DTOs.Common;
using Cms.Application.DTOs.Governance;

namespace Cms.Application.Interfaces.Governance;

public interface IPostIncidentReviewService
{
    /// <summary>
    /// Lists AI recommendations with the human decision, overrides and outcome.
    /// Used by the post-incident review screen.
    /// </summary>
    Task<PagedResult<ReviewEntryDto>> GetDecisionsAsync(
        Guid? crisisId = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        string? outcome = null);
}
