using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class Allocation : BaseEntity { public Guid RequestId {get;set;} public Guid InventoryId {get;set;} public decimal Quantity {get;set;} public Guid DecisionId {get;set;} public string Status {get;set;}="Allocated"; public string Explanation {get;set;}=""; public string IdempotencyKey {get;set;}=""; }
