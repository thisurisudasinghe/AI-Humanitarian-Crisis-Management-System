using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class Inventory : BaseEntity { public Guid ResourceTypeId {get;set;} public Guid OrgId {get;set;} public decimal Quantity {get;set;} public decimal Available {get;set;} public decimal Reserved {get;set;} public decimal Allocated {get;set;} public decimal Dispatched {get;set;} public decimal Utilised {get;set;} public decimal ReorderThreshold {get;set;} public DateTime? ExpiryDate {get;set;} public double? Lat {get;set;} public double? Lng {get;set;} public bool AccessibilityCapable {get;set;} }
