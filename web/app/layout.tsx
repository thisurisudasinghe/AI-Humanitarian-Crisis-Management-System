import type { Metadata } from "next";
import "leaflet/dist/leaflet.css";
import "./style.css";
export const metadata: Metadata = { title: "Crisis Operations", description: "Humanitarian logistics, shelters and volunteer operations" };
export default function RootLayout({children}:{children:React.ReactNode}) { return <html lang="en"><body><header><a href="/logistics">Logistics</a><a href="/shelters">Shelters</a><a href="/volunteer">Volunteer</a><a href="/maps">Operations map</a></header><main>{children}</main></body></html>; }
