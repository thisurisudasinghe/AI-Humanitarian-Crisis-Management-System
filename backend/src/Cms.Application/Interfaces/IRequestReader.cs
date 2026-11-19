using Cms.Application.DTOs;

namespace Cms.Application.Interfaces;

public interface IRequestReader
{
    Task<RequestSummaryDto?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
}