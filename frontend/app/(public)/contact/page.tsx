import type { Metadata } from "next";
import { InfoPageView } from "@/components/safeway/InfoPageView";

export const metadata: Metadata = {
  title: "Contact & support · SAFEWAY",
  description:
    "Ask questions and find the right support. In immediate danger, contact local emergency services first."
};

export default function ContactPage() {
  return <InfoPageView kind="contact" />;
}
