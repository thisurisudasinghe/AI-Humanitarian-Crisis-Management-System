export const RoleNames = {
  Citizen: "Citizen",
  EmergencyCoordinator: "EmergencyCoordinator",
  CaseManager: "CaseManager",
  LogisticsManager: "LogisticsManager",
  ShelterAdmin: "ShelterAdmin",
  VolunteerCoordinator: "VolunteerCoordinator",
  Volunteer: "Volunteer",
  DonorOfficer: "DonorOfficer",
  PartnerOfficer: "PartnerOfficer",
  SystemAdmin: "SystemAdmin",
  Donor: "Donor"
} as const;

export type RoleName = (typeof RoleNames)[keyof typeof RoleNames];

/** Public purposes a user may select. The server validates this allowlist. */
export const PublicPurposes = ["citizen", "volunteer", "donor"] as const;
export type PublicPurpose = (typeof PublicPurposes)[number];

export const PublicPurposeRoles: Record<PublicPurpose, RoleName> = {
  citizen: RoleNames.Citizen,
  volunteer: RoleNames.Volunteer,
  donor: RoleNames.Donor
};

export const AllRoles: RoleName[] = Object.values(RoleNames);

/** Roles that use the staff/operations workspace. Public participant roles are excluded. */
export const StaffRoles: RoleName[] = [
  RoleNames.EmergencyCoordinator,
  RoleNames.CaseManager,
  RoleNames.LogisticsManager,
  RoleNames.ShelterAdmin,
  RoleNames.VolunteerCoordinator,
  RoleNames.DonorOfficer,
  RoleNames.PartnerOfficer,
  RoleNames.SystemAdmin
];

export const PublicPortalRoles: RoleName[] = [
  RoleNames.Citizen,
  RoleNames.Volunteer,
  RoleNames.Donor
];

export function hasAnyRole(roles: readonly string[], allowed: readonly string[]): boolean {
  return roles.some((role) => allowed.includes(role));
}

export function hasRole(roles: readonly string[], role: string): boolean {
  return roles.includes(role);
}

export function isStaff(roles: readonly string[]): boolean {
  return hasAnyRole(roles, StaffRoles);
}

export function isPublicPortalUser(roles: readonly string[]): boolean {
  return !isStaff(roles) && hasAnyRole(roles, PublicPortalRoles);
}

/** Landing route that matches the roles returned by the identity API. */
export function homePathForRoles(roles: readonly string[]): string {
  return isStaff(roles) ? "/operations" : "/dashboard";
}
