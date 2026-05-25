import type { Metadata, Viewport } from "next";
import "./globals.css";
import { NavBar } from "@/components/NavBar";
import { SwrProvider } from "@/components/SwrProvider";

export const metadata: Metadata = {
  title: "DataViz — Система визуализации данных",
  description: "Курсовой проект: Next.js + ASP.NET Core + PostgreSQL",
};

export const viewport: Viewport = {
  themeColor: "#0f172a",
  width: "device-width",
  initialScale: 1,
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="ru">
      <body className="min-h-screen bg-slate-950 text-slate-100 antialiased">
        <SwrProvider>
          <NavBar />
          <main className="mx-auto max-w-7xl px-6 py-8">{children}</main>
        </SwrProvider>
      </body>
    </html>
  );
}
