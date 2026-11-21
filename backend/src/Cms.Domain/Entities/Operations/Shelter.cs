using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class Shelter : BaseEntity { public string Name {get;set;}=""; public int Capacity {get;set;} public int Occupancy {get;set;} public string Facilities {get;set;}="[]"; public string AccessibilityFeatures {get;set;}="[]"; public string Services {get;set;}="[]"; public double Lat {get;set;} public double Lng {get;set;} public Guid OrgId {get;set;} public bool Approved {get;set;} }
