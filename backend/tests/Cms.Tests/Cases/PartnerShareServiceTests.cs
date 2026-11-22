using Cms.Application.Cases.DTOs;
using Cms.Application.DTOs;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Domain.Exceptions;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cms.Tests.Cases;

public class PartnerShareServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 22, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid OrgA = Guid.Parse("c1000000-0000-0000-0000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("c1000000-0000-0000-0000-000000000002");

    [Fact]
    public async Task Share_creates_active_share_with_reader_status_and_audit()
    {
        using var context = TestContext.Create();
        await SeedOrganisationsAsync(context);
        var request = await SeedRequestAsync(context);
        var reader = CreateReader();
        var staff = Staff();
        var service = CreateService(context, staff, reader);

        var dto = await service.ShareAsync(new CreatePartnerShareDto
        {
            RequestId = request.Id,
            OrganisationId = OrgA
        });

        Assert.Equal(request.Id, dto.RequestId);
        Assert.Equal(OrgA, dto.OrganisationId);
        Assert.Equal(staff.UserId!.Value, dto.SharedByUserId);
        Assert.Equal(RequestStatus.Triaged, dto.Status);
        Assert.Equal(Category.Food, dto.Category);
        Assert.Null(dto.RevokedAt);

        var stored = await context.Set<PartnerShare>().SingleAsync();
        Assert.Equal(OrgA, stored.OrganisationId);
        Assert.Equal(Now, stored.SharedAt);
        Assert.Null(stored.RevokedAt);

        var audit = await context.AuditEvents.SingleAsync();
        Assert.Equal("PartnerShare.Created", audit.Action);
        Assert.Equal("PartnerShare", audit.EntityType);
        reader.Verify(r => r.GetAsync(request.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Share_denies_non_staff_users()
    {
        using var context = TestContext.Create();
        var request = await SeedRequestAsync(context);
        var service = CreateService(context, Partner(OrgA), CreateReader());

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.ShareAsync(
            new CreatePartnerShareDto { RequestId = request.Id, OrganisationId = OrgA }));

        Assert.Equal("PARTNER_SHARE_FORBIDDEN", ex.Code);
        Assert.Equal(0, await context.Set<PartnerShare>().CountAsync());
        Assert.Equal(0, await context.AuditEvents.CountAsync());
    }

    [Fact]
    public async Task Share_requires_an_existing_request()
    {
        using var context = TestContext.Create();
        var missing = Guid.NewGuid();
        var service = CreateService(context, Staff(), CreateReader((RequestSummaryDto?)null));

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.ShareAsync(
            new CreatePartnerShareDto { RequestId = missing, OrganisationId = OrgA }));

        Assert.Equal("REQUEST_NOT_FOUND", ex.Code);
        Assert.Equal(0, await context.Set<PartnerShare>().CountAsync());
    }

    [Fact]
    public async Task Share_requires_an_existing_organisation()
    {
        using var context = TestContext.Create();
        var request = await SeedRequestAsync(context);
        var service = CreateService(context, Staff(), CreateReader());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.ShareAsync(
            new CreatePartnerShareDto { RequestId = request.Id, OrganisationId = OrgB }));

        Assert.Equal("ORGANISATION_NOT_FOUND", ex.Code);
        Assert.Equal(0, await context.Set<PartnerShare>().CountAsync());
    }

    [Fact]
    public async Task Share_rejects_duplicate_active_share()
    {
        using var context = TestContext.Create();
        await SeedOrganisationsAsync(context);
        var request = await SeedRequestAsync(context);
        var service = CreateService(context, Staff(), CreateReader());
        var payload = new CreatePartnerShareDto { RequestId = request.Id, OrganisationId = OrgA };

        await service.ShareAsync(payload);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.ShareAsync(payload));

        Assert.Equal("DUPLICATE_ACTIVE_SHARE", ex.Code);
        Assert.Equal(1, await context.Set<PartnerShare>().CountAsync());
    }

    [Fact]
    public async Task PartnerOfficer_lists_only_own_active_shares_with_reader_status()
    {
        using var context = TestContext.Create();
        var ownRequest = await SeedRequestAsync(context);
        var otherRequest = await SeedRequestAsync(context);
        var thirdRequest = await SeedRequestAsync(context);
        var own = NewShare(ownRequest.Id, OrgA);
        var revoked = NewShare(otherRequest.Id, OrgA, Now.AddMinutes(-1));
        var foreign = NewShare(thirdRequest.Id, OrgB);
        await context.Set<PartnerShare>().AddRangeAsync(own, revoked, foreign);
        await context.SaveChangesAsync();
        var reader = CreateReader(RequestStatus.Recommended);
        var service = CreateService(context, Partner(OrgA), reader);

        var shares = await service.ListAsync();

        var share = Assert.Single(shares);
        Assert.Equal(own.Id, share.Id);
        Assert.Equal(OrgA, share.OrganisationId);
        Assert.Equal(RequestStatus.Recommended, share.Status);
        reader.Verify(r => r.GetAsync(ownRequest.Id, It.IsAny<CancellationToken>()), Times.Once);
        reader.Verify(r => r.GetAsync(thirdRequest.Id, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PartnerOfficer_without_organisation_is_denied()
    {
        using var context = TestContext.Create();
        var service = CreateService(context, Partner(null), CreateReader());

        var ex = await Assert.ThrowsAsync<ForbiddenException>(() => service.ListAsync());

        Assert.Equal("PARTNER_ORG_REQUIRED", ex.Code);
    }

    [Fact]
    public async Task PartnerOfficer_request_for_another_organisation_is_not_found()
    {
        using var context = TestContext.Create();
        var request = await SeedRequestAsync(context);
        await context.Set<PartnerShare>().AddAsync(NewShare(request.Id, OrgB));
        await context.SaveChangesAsync();
        var service = CreateService(context, Partner(OrgA), CreateReader());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => service.ListAsync(request.Id));

        Assert.Equal("PARTNER_SHARE_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task Staff_can_list_active_shares_for_a_request()
    {
        using var context = TestContext.Create();
        var request = await SeedRequestAsync(context);
        var share = NewShare(request.Id, OrgB);
        await context.Set<PartnerShare>().AddRangeAsync(share, NewShare(request.Id, OrgA, Now.AddMinutes(-1)));
        await context.SaveChangesAsync();
        var service = CreateService(context, Staff(), CreateReader());

        var shares = await service.ListAsync(request.Id);

        Assert.Equal(share.Id, Assert.Single(shares).Id);
    }

    [Fact]
    public async Task Revoke_marks_share_revoked_and_allows_a_fresh_share()
    {
        using var context = TestContext.Create();
        await SeedOrganisationsAsync(context);
        var request = await SeedRequestAsync(context);
        var service = CreateService(context, Staff(), CreateReader());
        var payload = new CreatePartnerShareDto { RequestId = request.Id, OrganisationId = OrgA };
        var first = await service.ShareAsync(payload);

        await service.RevokeAsync(first.Id);
        var second = await service.ShareAsync(payload);

        var rows = await context.Set<PartnerShare>().OrderBy(s => s.SharedAt).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(Now, rows[0].RevokedAt);
        Assert.Null(rows[1].RevokedAt);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(3, await context.AuditEvents.CountAsync());
        var auditActions = await context.AuditEvents.Select(a => a.Action).ToListAsync();
        Assert.Contains("PartnerShare.Revoked", auditActions);
    }

    [Fact]
    public async Task Revoke_denies_non_staff_and_rejects_a_repeat()
    {
        using var context = TestContext.Create();
        await SeedOrganisationsAsync(context);
        var request = await SeedRequestAsync(context);
        var staffService = CreateService(context, Staff(), CreateReader());
        var share = await staffService.ShareAsync(new CreatePartnerShareDto
        {
            RequestId = request.Id,
            OrganisationId = OrgA
        });

        var partnerService = CreateService(context, Partner(OrgA), CreateReader());
        var forbidden = await Assert.ThrowsAsync<ForbiddenException>(() => partnerService.RevokeAsync(share.Id));
        Assert.Equal("PARTNER_SHARE_FORBIDDEN", forbidden.Code);

        await staffService.RevokeAsync(share.Id);
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => staffService.RevokeAsync(share.Id));
        Assert.Equal("SHARE_ALREADY_REVOKED", conflict.Code);
    }

    private static async Task SeedOrganisationsAsync(CmsDbContext context)
    {
        await context.Organisations.AddRangeAsync(
            new Cms.Domain.Entities.Organisation { Id = OrgA, Name = "Partner A" },
            new Cms.Domain.Entities.Organisation { Id = OrgB, Name = "Partner B" });
        await context.SaveChangesAsync();
    }

    private static PartnerShare NewShare(Guid requestId, Guid organisationId, DateTime? revokedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        RequestId = requestId,
        OrganisationId = organisationId,
        SharedByUserId = Guid.NewGuid(),
        SharedAt = Now.AddMinutes(-2),
        RevokedAt = revokedAt,
        CreatedAt = Now.AddMinutes(-2)
    };

    private static async Task<Request> SeedRequestAsync(CmsDbContext context)
    {
        var request = TestContext.NewRequest(Guid.NewGuid());
        await context.Set<Request>().AddAsync(request);
        await context.SaveChangesAsync();
        return request;
    }

    private static FakeCurrentUser Staff() => new()
    {
        UserId = Guid.Parse("c2000000-0000-0000-0000-000000000001"),
        Roles = new[] { CasesRoles.EmergencyCoordinator }
    };

    private static FakeCurrentUser Partner(Guid? organisationId) => new()
    {
        UserId = Guid.Parse("c2000000-0000-0000-0000-000000000002"),
        OrgId = organisationId,
        Roles = new[] { CasesRoles.PartnerOfficer }
    };

    private static Mock<IRequestReader> CreateReader(
        RequestStatus status = RequestStatus.Triaged) => CreateReader(summary: new RequestSummaryDto
    {
        Id = Guid.NewGuid(),
        Status = status,
        Category = Category.Food,
        Urgency = Urgency.High
    });

    private static Mock<IRequestReader> CreateReader(RequestSummaryDto? summary)
    {
        var reader = new Mock<IRequestReader>();
        reader
            .Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, CancellationToken _) => summary is null
                ? null
                : new RequestSummaryDto
                {
                    Id = id,
                    Status = summary.Status,
                    Category = summary.Category,
                    Urgency = summary.Urgency,
                    Description = summary.Description,
                    CreatedAt = summary.CreatedAt
                });
        return reader;
    }

    private static PartnerShareService CreateService(
        CmsDbContext context,
        FakeCurrentUser user,
        Mock<IRequestReader> reader)
    {
        var date = new FixedDateTimeService(Now);
        return new PartnerShareService(
            context,
            user,
            date,
            reader.Object,
            TestContext.CreateWriter(context, date));
    }
}
