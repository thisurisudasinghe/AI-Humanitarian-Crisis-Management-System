using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class VolunteerActivity : BaseEntity { public Guid VolunteerId {get;set;} public Guid TaskId {get;set;} public Guid CompletedBy {get;set;} public DateTime CompletedAt {get;set;} }
