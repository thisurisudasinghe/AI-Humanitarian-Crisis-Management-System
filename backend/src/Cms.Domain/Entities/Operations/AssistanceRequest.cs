using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class AssistanceRequest : BaseEntity { public Guid FromOrgId {get;set;} public Guid? ToOrgId {get;set;} public Guid ResourceTypeId {get;set;} public decimal Quantity {get;set;} public string Reason {get;set;}=""; public string Status {get;set;}="Pending"; public Guid? ShelterId {get;set;} public string? ResponseReason {get;set;} }
