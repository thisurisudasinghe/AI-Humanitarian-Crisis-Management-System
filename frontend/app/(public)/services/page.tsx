import type { Metadata } from "next";
import { InfoPageView } from "@/components/safeway/InfoPageView";

export const metadata: Metadata = {
  title: "Services · SAFEWAY",
  description: "Public assistance and authorized response tools connected through one platform."
};

export default function ServicesPage() {
  return <InfoPageView kind="services" />;
}
