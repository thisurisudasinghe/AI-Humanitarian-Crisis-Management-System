import type { Metadata } from "next";
import { ReportFlow } from "@/components/safeway/ReportFlow";

export const metadata: Metadata = {
  title: "Report a crisis · SAFEWAY",
  description: "Share what is happening and where help is needed."
};

export default function ReportPage() {
  return <ReportFlow />;
}
