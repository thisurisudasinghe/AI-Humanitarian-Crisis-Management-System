using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class VolunteerAvailability : BaseEntity { public Guid VolunteerId {get;set;} public DateTime StartsAt {get;set;} public DateTime EndsAt {get;set;} }
