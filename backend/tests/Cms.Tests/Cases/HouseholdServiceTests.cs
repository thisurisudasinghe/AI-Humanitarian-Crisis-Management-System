using Cms.Application.Cases.DTOs;
using Cms.Domain.Entities.Cases;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

public class HouseholdServiceTests
{
    private const string Phone = "+94771234567";
    private const string Needs = "Wheelchair and oxygen";

    [Fact]
    public async Task Save_and_read_mine_encrypts_at_rest_and_masks_audit()
    {
        using var context = TestContext.Create();
        var user = new FakeCurrentUser();
        var now = new DateTime(2026, 11, 18, 11, 0, 0, DateTimeKind.Utc);
        var protector = TestContext.CreateProtector();
        var service = CreateService(context, user, now, protector);

        var id = await service.UpsertMineAsync(new SaveHouseholdDto
        {
            Size = 4,
            ContactPhone = Phone,
            AccessibilityNeeds = Needs
        });

        var stored = await context.Set<Household>().SingleAsync(h => h.Id == id);
        Assert.NotEqual(Phone, stored.ContactPhone);
        Assert.NotEqual(Needs, stored.AccessibilityNeeds);
        Assert.DoesNotContain("771234567", stored.ContactPhone!);
        Assert.DoesNotContain(Needs, stored.AccessibilityNeeds!);

        var audit = await context.AuditLogs.SingleAsync();
        Assert.DoesNotContain("771234567", audit.Details!);
        Assert.DoesNotContain("Wheelchair", audit.Details!);
        Assert.Contains("***", audit.Details!);

        var dto = await service.GetMineAsync();
        Assert.NotNull(dto);
        Assert.Equal(4, dto!.Size);
        Assert.Equal(Phone, dto.ContactPhone);
        Assert.Equal(Needs, dto.AccessibilityNeeds);
    }

    [Fact]
    public async Task Another_citizen_does_not_see_the_household()
    {
        using var context = TestContext.Create();
        var owner = new FakeCurrentUser();
        var now = DateTime.UtcNow;
        var protector = TestContext.CreateProtector();
        await CreateService(context, owner, now, protector).UpsertMineAsync(new SaveHouseholdDto
        {
            Size = 2,
            ContactPhone = Phone,
            AccessibilityNeeds = Needs
        });

        var other = new FakeCurrentUser { UserId = Guid.NewGuid() };
        var dto = await CreateService(context, other, now, protector).GetMineAsync();

        Assert.Null(dto);
    }

    private static HouseholdService CreateService(
        ApplicationDbContext context,
        FakeCurrentUser user,
        DateTime now,
        Cms.Application.Cases.Interfaces.ISensitiveDataProtector protector)
    {
        var date = new FixedDateTimeService(now);
        var options = TestContext.Options();
        return new HouseholdService(context, user, date, protector, new CaseWriteHelper(context, date, options));
    }
}
