"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { getToken, getUser, subscribeAuth, type User } from "@/lib/api";
import type { UserRole } from "@/lib/types";

type AuthGuardProps = {
  children: React.ReactNode;
  /** Если указано, доступ разрешается только пользователям с этой ролью. */
  requireRole?: UserRole;
};

/**
 * Защищённая обёртка для страниц: проверяет наличие JWT и (опционально) роль,
 * перенаправляет на /login или /unauthorized при отсутствии доступа.
 */
export function AuthGuard({ children, requireRole }: AuthGuardProps) {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [checked, setChecked] = useState(false);

  useEffect(() => {
    const verify = () => {
      const token = getToken();
      const u = getUser();
      if (!token || !u) {
        router.replace("/login");
        return;
      }
      if (requireRole && u.role !== requireRole) {
        router.replace("/unauthorized");
        return;
      }
      setUser(u);
      setChecked(true);
    };
    verify();
    return subscribeAuth(verify);
  }, [router, requireRole]);

  if (!checked || !user) {
    return (
      <div
        role="status"
        aria-live="polite"
        className="flex h-screen w-full items-center justify-center text-slate-400"
      >
        Загрузка...
      </div>
    );
  }

  return <>{children}</>;
}
