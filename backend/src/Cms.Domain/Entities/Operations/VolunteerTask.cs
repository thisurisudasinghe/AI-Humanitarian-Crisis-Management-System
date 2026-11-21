using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class VolunteerTask : BaseEntity { public Guid? RequestId {get;set;} public string Title {get;set;}=""; public string RequiredSkillsJson {get;set;}="[]"; public double? Lat {get;set;} public double? Lng {get;set;} public DateTime? ScheduledAt {get;set;} public string Status {get;set;}="Open"; public Guid? VolunteerId {get;set;} public DateTime? CompletedAt {get;set;} }
