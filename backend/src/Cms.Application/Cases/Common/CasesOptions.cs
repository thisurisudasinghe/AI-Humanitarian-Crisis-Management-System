using Cms.Domain.Enums.Cases;

namespace Cms.Application.Cases.Common;

public class CasesOptions
{
    public int SlaHours { get; set; } = 24;
    public string UploadRoot { get; set; } = "App_Data/case-uploads";
    public string DataProtectionKeyRing { get; set; } = "App_Data/data-protection-keys";
    public string DefaultLanguage { get; set; } = "en";

    public IReadOnlyCollection<Category> ApprovedCategories { get; set; } =
        Enum.GetValues<Category>();
}
