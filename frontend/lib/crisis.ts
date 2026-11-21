/**
 * Crisis intake is owned by Dev 2 (crisis & case management).
 *
 * The Shared Contract v3/v4 describes the crisis, case, request and assignment
 * modules, but those controllers are not part of the Dev 1 backend yet. This
 * module is the single integration seam for that work: when Dev 2 ships the
 * endpoints, replace the stub below with the real calls and the report flow
 * will submit without further UI changes.
 *
 * Nothing here guesses table names or invents endpoint paths.
 */

export class FeatureNotAvailableError extends Error {
  readonly feature: string;
  readonly owner: string;

  constructor(feature: string, owner: string) {
    super(`${feature} is not available yet. Owned by ${owner}.`);
    this.name = "FeatureNotAvailableError";
    this.feature = feature;
    this.owner = owner;
  }
}

export interface CrisisReportInput {
  category: string;
  description: string;
  location: string;
  urgency: string;
  firstName: string;
  lastName: string;
  phone: string;
  email?: string;
  consent: boolean;
}

export interface CrisisReportReceipt {
  reference: string;
  submittedAt: string;
}

export const crisisModuleOwner = "Dev 2 — crisis & case management";

/**
 * Submits a public crisis report.
 *
 * Intentionally fails closed until the Dev 2 intake endpoint exists; the UI
 * renders a "not submitted" state instead of pretending the report was stored.
 */
export async function submitCrisisReport(
  _input: CrisisReportInput
): Promise<CrisisReportReceipt> {
  throw new FeatureNotAvailableError("Crisis reporting", crisisModuleOwner);
}
