using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class ShelterAllocation : BaseEntity { public Guid RequestId {get;set;} public Guid ShelterId {get;set;} public int HouseholdSize {get;set;} public Guid DecisionId {get;set;} }
