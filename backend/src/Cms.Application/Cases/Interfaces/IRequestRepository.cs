using Cms.Application.Cases.Common;
using Cms.Application.Cases.DTOs;
using Cms.Domain.Entities.Cases;

namespace Cms.Application.Cases.Interfaces;

public interface IRequestRepository
{
    Task<Request?> GetByIdAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<Request?> GetByIdForUpdateAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<PagedResult<Request>> ListAsync(
        RequestFilter filter,
        PagedQuery query,
        CancellationToken cancellationToken = default);
    Task AddAsync(Request request, CancellationToken cancellationToken = default);
}
