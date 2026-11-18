namespace Cms.Domain.Constants;

/// <summary>Frozen team contract values. Do not add alternative spellings here.</summary>
public static class Roles
{
    public const string Citizen = "Citizen";
    public const string EmergencyCoordinator = "EmergencyCoordinator";
    public const string CaseManager = "CaseManager";
    public const string LogisticsManager = "LogisticsManager";
    public const string ShelterAdmin = "ShelterAdmin";
    public const string VolunteerCoordinator = "VolunteerCoordinator";
    public const string Volunteer = "Volunteer";
    public const string DonorOfficer = "DonorOfficer";
    public const string PartnerOfficer = "PartnerOfficer";
    public const string SystemAdmin = "SystemAdmin";

    public static readonly string[] All =
    {
        Citizen, EmergencyCoordinator, CaseManager, LogisticsManager,
        ShelterAdmin, VolunteerCoordinator, Volunteer, DonorOfficer,
        PartnerOfficer, SystemAdmin
    };
}

public static class RecommendationTypes
{
    public const string Classification = "Classification";
    public const string Priority = "Priority";
    public const string Duplicate = "Duplicate";
    public const string ResourceMatch = "ResourceMatch";
    public const string ShelterMatch = "ShelterMatch";
    public const string VolunteerMatch = "VolunteerMatch";
    public const string Anomaly = "Anomaly";
    public const string Summary = "Summary";
    public const string Announcement = "Announcement";
    public const string CloseCase = "CloseCase";

    public static readonly string[] All =
    {
        Classification, Priority, Duplicate, ResourceMatch, ShelterMatch,
        VolunteerMatch, Anomaly, Summary, Announcement, CloseCase
    };
}

public static class RecommendationSources
{
    public const string Ai = "Ai";
    public const string Rule = "Rule";

    public static readonly string[] All = { Ai, Rule };
}

public static class OutboxTypes
{
    public const string RequestSubmitted = "Request.Submitted";
    public const string NotificationSend = "Notification.Send";
}

public static class NotificationChannels
{
    public const string Email = "Email";
    public const string InApp = "InApp";
}
