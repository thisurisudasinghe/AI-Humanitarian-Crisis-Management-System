/**
 * Verified Sri Lankan emergency contact details for public-facing pages.
 *
 * Labels and explanatory copy stay in the locale dictionaries. Values here are
 * deliberately limited to phone numbers, tel: links and official source URLs so
 * a number is never duplicated across pages with a typo.
 *
 * SAFEWAY is a coordination tool. It does not replace official emergency
 * services and cannot guarantee immediate rescue or dispatch.
 */

export interface EmergencySource {
  id: string;
  name: string;
  url: string;
}

export const emergencySources = {
  police: "https://www.police.lk/",
  ambulance: "https://www.1990.lk/",
  stateHealth: "https://www.statehealth.gov.lk/",
  tourismEmergency: "https://www.srilanka.travel/emergency-services",
  fireService: "https://www.colombo.mc.gov.lk/fire-services.php",
  disasterManagement: "https://www.dmc.gov.lk/"
} as const;

export const emergencySourceList: EmergencySource[] = [
  { id: "police", name: "Sri Lanka Police", url: emergencySources.police },
  { id: "ambulance", name: "1990 Suwa Seriya", url: emergencySources.ambulance },
  { id: "stateHealth", name: "Ministry of Health", url: emergencySources.stateHealth },
  { id: "tourismEmergency", name: "Sri Lanka Tourism", url: emergencySources.tourismEmergency },
  { id: "fireService", name: "Fire Service Department listing", url: emergencySources.fireService },
  { id: "disasterManagement", name: "Disaster Management Centre", url: emergencySources.disasterManagement }
];

export interface EmergencyNumber {
  display: string;
  href: string;
}

export interface EmergencyContact {
  id: "police" | "ambulance" | "fire" | "disaster";
  labelKey: string;
  descriptionKey: string;
  icon: "shield" | "medical" | "alert" | "info";
  primary: EmergencyNumber;
  fallbacks: EmergencyNumber[];
  sourceIds: string[];
}

function emergencyNumber(display: string, dial: string): EmergencyNumber {
  return { display, href: `tel:${dial}` };
}

export const emergencyContacts: EmergencyContact[] = [
  {
    id: "police",
    labelKey: "pw.emergency.police",
    descriptionKey: "pw.emergency.policeHelp",
    icon: "shield",
    primary: emergencyNumber("119", "119"),
    fallbacks: [],
    sourceIds: ["police"]
  },
  {
    id: "ambulance",
    labelKey: "pw.emergency.ambulance",
    descriptionKey: "pw.emergency.ambulanceHelp",
    icon: "medical",
    primary: emergencyNumber("1990", "1990"),
    fallbacks: [],
    sourceIds: ["ambulance", "stateHealth"]
  },
  {
    id: "fire",
    labelKey: "pw.emergency.fire",
    descriptionKey: "pw.emergency.fireHelp",
    icon: "alert",
    primary: emergencyNumber("110", "110"),
    fallbacks: [
      emergencyNumber("011 242 2222", "0112422222"),
      emergencyNumber("011 242 2223", "0112422223")
    ],
    sourceIds: ["tourismEmergency", "fireService"]
  },
  {
    id: "disaster",
    labelKey: "pw.emergency.disaster",
    descriptionKey: "pw.emergency.disasterHelp",
    icon: "info",
    primary: emergencyNumber("117", "117"),
    fallbacks: [],
    sourceIds: ["disasterManagement"]
  }
];

export const SAFEWAY_EMERGENCY_LIMITATION_KEY = "pw.emergency.limitation";

export function emergencySourcesFor(contact: EmergencyContact): EmergencySource[] {
  return contact.sourceIds
    .map((id) => emergencySourceList.find((source) => source.id === id))
    .filter((source): source is EmergencySource => Boolean(source));
}
