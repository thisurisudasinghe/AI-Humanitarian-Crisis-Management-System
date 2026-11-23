import "leaflet/dist/leaflet.css";
import type { Metadata } from "next";
import type { ReactNode } from "react";
import { AuthProvider } from "@/lib/auth";
import { I18nProvider } from "@/lib/i18n";
import { GlobalFloatingChat } from "@/components/safeway/GlobalFloatingChat";
import "./globals.css";
import "./global-chatbot.css";

export const metadata: Metadata = {
  title: "SAFEWAY - Humanitarian response network",
  description:
    "Report a crisis, find verified assistance and coordinate humanitarian response with human oversight.",
  applicationName: "SAFEWAY"
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body>
        <I18nProvider>
          <AuthProvider>
            {children}
            <GlobalFloatingChat />
          </AuthProvider>
        </I18nProvider>
      </body>
    </html>
  );
}
