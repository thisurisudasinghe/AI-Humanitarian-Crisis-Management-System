import "leaflet/dist/leaflet.css";
import type { Metadata } from "next";
import type { ReactNode } from "react";
import { AuthProvider } from "@/lib/auth";
import { GlobalFloatingChat } from "@/components/safeway/GlobalFloatingChat";
import "./globals.css";
import "./global-chatbot.css";

export const metadata: Metadata = {
  title: "SAFEWAY · Humanitarian response network",
  description:
    "Report a crisis, find verified assistance and coordinate humanitarian response with human oversight.",
  applicationName: "SAFEWAY"
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en">
      <body>
        <AuthProvider>
          {children}
          <GlobalFloatingChat />
        </AuthProvider>
      </body>
    </html>
  );
}
