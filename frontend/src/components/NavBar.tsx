"use client";

import Link from "next/link";
import { useRouter, usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { clearAuth, getUser, User } from "@/lib/api";

const links = [
  { href: "/dashboard", label: "Дашборд" },
  { href: "/products", label: "Товары" },
  { href: "/orders", label: "Заказы" },
];

export function NavBar() {
  const router = useRouter();
  const pathname = usePathname();
  const [user, setUser] = useState<User | null>(null);

  useEffect(() => {
    setUser(getUser());
  }, [pathname]);

  if (!user) return null;

  const logout = () => {
    clearAuth();
    router.push("/login");
  };

  return (
    <nav className="border-b border-slate-800 bg-slate-950">
      <div className="mx-auto flex max-w-7xl items-center justify-between px-6 py-3">
        <Link href="/dashboard" className="text-lg font-semibold text-white">
          DataViz
        </Link>
        <div className="flex items-center gap-6">
          {links.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className={`text-sm transition-colors ${
                pathname?.startsWith(l.href)
                  ? "text-white"
                  : "text-slate-400 hover:text-white"
              }`}
            >
              {l.label}
            </Link>
          ))}
          <div className="flex items-center gap-3 border-l border-slate-800 pl-6">
            <span className="text-sm text-slate-400">{user.name}</span>
            <button
              onClick={logout}
              className="rounded-md border border-slate-700 px-3 py-1 text-sm text-slate-200 transition-colors hover:bg-slate-800"
            >
              Выйти
            </button>
          </div>
        </div>
      </div>
    </nav>
  );
}
