using Cms.Domain.Common;
namespace Cms.Domain.Entities.Operations;
public class ResourceType : BaseEntity { public string Name {get;set;}=""; public string Unit {get;set;}="units"; public string? Description {get;set;} }
