import type { Metadata } from "next";
import { InfoPageView } from "@/components/safeway/InfoPageView";

export const metadata: Metadata = {
  title: "About · SAFEWAY",
  description: "How SAFEWAY keeps people, judgment and accountability at the centre of crisis response."
};

export default function AboutPage() {
  return <InfoPageView kind="about" />;
}
