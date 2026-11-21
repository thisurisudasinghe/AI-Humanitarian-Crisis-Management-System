namespace Cms.Application.Cases.Interfaces;

public interface IOverduePolicy
{
    int SlaHours { get; }
    bool IsOverdue(DateTime submittedAtUtc, DateTime nowUtc);
    int AgeHours(DateTime submittedAtUtc, DateTime nowUtc);
}
