using Cms.Application.Cases.Common;
using Cms.Application.Cases.Interfaces;
using Cms.Application.Interfaces;
using Cms.Domain.Cases;
using Cms.Domain.Entities.Cases;
using Cms.Domain.Enums.Cases;
using Cms.Infrastructure.Cases;
using Cms.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Cms.Tests.Cases;

internal sealed class FixedDateTimeService : IDateTimeService
{
    public FixedDateTimeService(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; }
}

internal sealed class FakeCurrentUser : ICurrentUserService
{
    public Guid? UserId { get; set; } = Guid.NewGuid();
    public string? Email { get; set; } = "test@example.invalid";
    public IReadOnlyCollection<string> Roles { get; set; } = new[] { CasesRoles.Citizen };
    public bool IsAuthenticated => UserId.HasValue;
}

internal sealed class NoOpDecisionGate : ICloseCaseDecisionGate
{
    public bool Approved { get; set; }
    public Task<bool> IsApprovedAsync(Guid decisionId, Guid requestId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Approved);
}

internal sealed class MemoryFileStore : IFileStore
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public Task SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        using var memory = new MemoryStream();
        content.CopyTo(memory);
        Files[storedFileName] = memory.ToArray();
        return Task.CompletedTask;
    }

    public Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default) =>
        Task.FromResult<Stream>(new MemoryStream(Files[storedFileName]));
}

internal static class TestContext
{
    public static ApplicationDbContext Create() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"cases-tests-{Guid.NewGuid():N}")
            .Options);

    public static CasesOptions Options() => new()
    {
        SlaHours = 24,
        DefaultLanguage = "en",
        UploadRoot = Path.Combine(Path.GetTempPath(), $"cms-case-tests-{Guid.NewGuid():N}"),
        DataProtectionKeyRing = Path.Combine(Path.GetTempPath(), $"cms-dp-tests-{Guid.NewGuid():N}")
    };

    public static SensitiveDataProtector CreateProtector() =>
        new(DataProtectionProvider.Create("Cms.Tests.Cases"));

    public static RequestStatusService CreateStatusService(
        ApplicationDbContext context,
        ICurrentUserService user,
        DateTime now,
        ICloseCaseDecisionGate? decisionGate = null)
    {
        var date = new FixedDateTimeService(now);
        var options = Options();
        return new RequestStatusService(
            context,
            user,
            date,
            new CaseWriteHelper(context, date, options),
            decisionGate ?? new NoOpDecisionGate());
    }

    public static Request NewRequest(
        Guid userId,
        RequestStatus status = RequestStatus.Submitted,
        Urgency urgency = Urgency.High,
        DateTime? submittedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        RequesterUserId = userId,
        Category = Category.Food,
        Urgency = urgency,
        Description = "Test request",
        Lat = 6.9m,
        Lng = 79.9m,
        HouseholdSize = 3,
        Status = status,
        SubmittedAt = submittedAt ?? DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow
    };
}
