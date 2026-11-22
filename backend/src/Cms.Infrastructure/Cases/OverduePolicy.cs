using Cms.Application.Cases.Common;
using Cms.Application.Cases.Interfaces;

namespace Cms.Infrastructure.Cases;

public sealed class OverduePolicy : IOverduePolicy
{
    public OverduePolicy(CasesOptions options)
    {
        SlaHours = options.SlaHours <= 0 ? 24 : options.SlaHours;
    }

    public int SlaHours { get; }

    public int AgeHours(DateTime submittedAtUtc, DateTime nowUtc) =>
        (int)Math.Max(0, Math.Floor((nowUtc - submittedAtUtc).TotalHours));

    public bool IsOverdue(DateTime submittedAtUtc, DateTime nowUtc) =>
        nowUtc - submittedAtUtc > TimeSpan.FromHours(SlaHours);
}
