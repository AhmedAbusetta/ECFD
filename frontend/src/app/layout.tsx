import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "ECFD",
  description: "Live call protection",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-page text-ink font-sans antialiased">{children}</body>
    </html>
  );
}
