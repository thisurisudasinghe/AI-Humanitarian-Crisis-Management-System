using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class Dispatch : BaseEntity { public Guid AllocationId {get;set;} public decimal Quantity {get;set;} public DateTime? ConfirmedAt {get;set;} public Guid? ConfirmedBy {get;set;} public string Status {get;set;}="Dispatched"; }
