namespace Cms.Domain.Constants;

/// <summary>Canonical keys stored in common.SystemSettings.</summary>
public static class SystemSettingKeys
{
    public const string SlaHours = "SlaHours";
    public const string ReorderThreshold = "ReorderThreshold";
    public const string ServiceTypes = "ServiceTypes";
    public const string RequestCategories = "RequestCategories";
    public const string UrgencyThresholds = "UrgencyThresholds";
    public const string SuspiciousDonationAmount = "SuspiciousDonationAmount";

    public static readonly IReadOnlyCollection<string> All =
    [
        SlaHours,
        ReorderThreshold,
        ServiceTypes,
        RequestCategories,
        UrgencyThresholds,
        SuspiciousDonationAmount
    ];
}
