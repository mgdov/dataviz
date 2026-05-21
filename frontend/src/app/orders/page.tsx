"use client";

import useSWR from "swr";
import { AuthGuard } from "@/components/AuthGuard";
import { swrFetcher } from "@/lib/api";
import { Order } from "@/lib/types";

const fmtRub = (n: number) =>
  new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(n) + " ₽";

const fmtDate = (s: string) =>
  new Date(s).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" });

export default function OrdersPage() {
  return (
    <AuthGuard>
      <OrdersInner />
    </AuthGuard>
  );
}

function OrdersInner() {
  const { data, isLoading } = useSWR<Order[]>("/api/orders", swrFetcher);

  return (
    <div className="space-y-6">
      <header>
        <h1 className="text-2xl font-semibold text-white">Заказы</h1>
        <p className="text-sm text-slate-400">Заказы текущего пользователя</p>
      </header>

      <div className="rounded-xl border border-slate-800 bg-slate-900">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-800 text-left text-slate-400">
              <th className="px-5 py-2 font-medium">#</th>
              <th className="px-5 py-2 font-medium">Дата</th>
              <th className="px-5 py-2 font-medium">Регион</th>
              <th className="px-5 py-2 font-medium">Позиции</th>
              <th className="px-5 py-2 text-right font-medium">Сумма</th>
            </tr>
          </thead>
          <tbody>
            {(data ?? []).map((o) => (
              <tr
                key={o.id}
                className="border-b border-slate-800/40 last:border-b-0"
              >
                <td className="px-5 py-2 text-slate-300">{o.id}</td>
                <td className="px-5 py-2 text-slate-300">
                  {fmtDate(o.createdAt)}
                </td>
                <td className="px-5 py-2 text-slate-300">{o.regionCode}</td>
                <td className="px-5 py-2 text-slate-300">
                  {o.items.length} шт.
                </td>
                <td className="px-5 py-2 text-right text-white">
                  {fmtRub(o.totalPrice)}
                </td>
              </tr>
            ))}
            {isLoading && (
              <tr>
                <td colSpan={5} className="px-5 py-6 text-center text-slate-500">
                  Загрузка...
                </td>
              </tr>
            )}
            {!isLoading && (data?.length ?? 0) === 0 && (
              <tr>
                <td colSpan={5} className="px-5 py-6 text-center text-slate-500">
                  Нет заказов
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
