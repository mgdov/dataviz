"use client";

import { SWRConfig } from "swr";
import { swrFetcher } from "@/lib/api";

/**
 * Глобальные настройки SWR: единый fetcher, разумные таймауты, чтобы не дёргать API
 * без причины, и оставление предыдущих данных при перезагрузке.
 */
export function SwrProvider({ children }: { children: React.ReactNode }) {
  return (
    <SWRConfig
      value={{
        fetcher: swrFetcher,
        revalidateOnFocus: false,
        dedupingInterval: 5000,
        errorRetryCount: 2,
        shouldRetryOnError: (err) => {
          const status = (err as { status?: number } | null)?.status;
          // не дёргаемся снова, если бэк ответил 4xx — это не сетевая ошибка
          return !(typeof status === "number" && status >= 400 && status < 500);
        },
      }}
    >
      {children}
    </SWRConfig>
  );
}
