using Cms.Domain.Common;
namespace Cms.Domain.Entities.Mapping;
public class GeoCache : BaseEntity { public string QueryKey {get;set;}=""; public string ResultJson {get;set;}=""; public DateTime ExpiresAt {get;set;} public bool IsFailure {get;set;} }
