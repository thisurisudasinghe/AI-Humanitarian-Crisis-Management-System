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
  SystemAdmin: "SystemAdmin"
} as const;

export type RoleName = (typeof RoleNames)[keyof typeof RoleNames];

export const AllRoles: RoleName[] = Object.values(RoleNames);

export const StaffRoles: RoleName[] = AllRoles.filter(
  (role) => role !== RoleNames.Citizen
);

export function hasAnyRole(roles: readonly string[], allowed: readonly string[]): boolean {
  return roles.some((role) => allowed.includes(role));
}

export function isStaff(roles: readonly string[]): boolean {
  return hasAnyRole(roles, StaffRoles);
}

/** Landing route that matches the roles returned by the identity API. */
export function homePathForRoles(roles: readonly string[]): string {
  return isStaff(roles) ? "/operations" : "/dashboard";
}
