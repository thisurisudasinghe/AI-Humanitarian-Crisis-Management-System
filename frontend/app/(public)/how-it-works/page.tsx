import type { Metadata } from "next";
import { InfoPageView } from "@/components/safeway/InfoPageView";

export const metadata: Metadata = {
  title: "How it works · SAFEWAY",
  description: "From a report to a responsible response, every step stays visible."
};

export default function HowItWorksPage() {
  return <InfoPageView kind="how" />;
}
