import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "ECFD Security Console",
  description: "Live conversational fraud monitoring dashboard",
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en">
      <body className="bg-slate-950">{children}</body>
    </html>
  );
}
