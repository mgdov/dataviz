"use client";

import { useMemo, useState } from "react";
import useSWR from "swr";
import { AuthGuard } from "@/components/AuthGuard";
import { KpiCard } from "@/components/KpiCard";
import { Plot } from "@/components/Plot";
import { swrFetcher } from "@/lib/api";
import {
  Category,
  RegionCategoryPoint,
  SalesDashboard,
} from "@/lib/types";

const REGIONS = ["MSK", "SPB", "NSK", "EKB", "KZN", "RND"];

const fmtRub = (n: number) =>
  new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(n) + " ₽";
const fmtInt = (n: number) =>
  new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(n);

function defaultDates() {
  const now = new Date();
  const from = new Date(now);
  from.setDate(from.getDate() - 90);
  return {
    from: from.toISOString().slice(0, 10),
    to: now.toISOString().slice(0, 10),
  };
}

export default function DashboardPage() {
  return (
    <AuthGuard>
      <DashboardInner />
    </AuthGuard>
  );
}

function DashboardInner() {
  const initial = useMemo(defaultDates, []);
  const [from, setFrom] = useState(initial.from);
  const [to, setTo] = useState(initial.to);
  const [regions, setRegions] = useState<string[]>([]);
  const [categoryId, setCategoryId] = useState<number | "">("");

  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  if (regions.length) params.set("regions", regions.join(","));
  if (categoryId !== "") params.set("categoryId", String(categoryId));

  const { data: categories } = useSWR<Category[]>(
    "/api/categories",
    swrFetcher,
  );
  const { data, error, isLoading } = useSWR<SalesDashboard>(
    `/api/dashboard/sales?${params.toString()}`,
    swrFetcher,
    { keepPreviousData: true },
  );

  const toggleRegion = (r: string) =>
    setRegions((prev) =>
      prev.includes(r) ? prev.filter((x) => x !== r) : [...prev, r],
    );

  return (
    <div className="space-y-6">
      <header className="flex items-end justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-white">Дашборд продаж</h1>
          <p className="text-sm text-slate-400">
            Интерактивная визуализация продаж по периодам, регионам и категориям
          </p>
        </div>
      </header>

      <section className="rounded-xl border border-slate-800 bg-slate-900 p-5">
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          <div>
            <label className="mb-1 block text-xs uppercase text-slate-400">с</label>
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-slate-400">по</label>
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            />
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-slate-400">
              Категория
            </label>
            <select
              value={categoryId}
              onChange={(e) =>
                setCategoryId(e.target.value === "" ? "" : Number(e.target.value))
              }
              className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
            >
              <option value="">Все</option>
              {categories?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label className="mb-1 block text-xs uppercase text-slate-400">
              Регионы
            </label>
            <div className="flex flex-wrap gap-2">
              {REGIONS.map((r) => {
                const active = regions.includes(r);
                return (
                  <button
                    key={r}
                    type="button"
                    onClick={() => toggleRegion(r)}
                    className={`rounded-md border px-2.5 py-1 text-xs transition-colors ${
                      active
                        ? "border-blue-500 bg-blue-600 text-white"
                        : "border-slate-700 bg-slate-950 text-slate-300 hover:bg-slate-800"
                    }`}
                  >
                    {r}
                  </button>
                );
              })}
            </div>
          </div>
        </div>
      </section>

      {error && (
        <div className="rounded-md border border-red-900 bg-red-950 px-4 py-3 text-sm text-red-300">
          Ошибка загрузки данных: {(error as Error).message}
        </div>
      )}

      <section className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <KpiCard
          title="Выручка"
          value={fmtRub(data?.kpi.revenue ?? 0)}
          hint={isLoading ? "Загрузка..." : "За выбранный период"}
        />
        <KpiCard title="Заказы" value={fmtInt(data?.kpi.ordersCount ?? 0)} />
        <KpiCard
          title="Средний чек"
          value={fmtRub(data?.kpi.averageOrderValue ?? 0)}
        />
        <KpiCard
          title="Покупатели"
          value={fmtInt(data?.kpi.uniqueCustomers ?? 0)}
          hint="Уникальных"
        />
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-3">
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4 lg:col-span-2">
          <h2 className="mb-3 text-sm font-medium uppercase tracking-wide text-slate-400">
            Динамика выручки
          </h2>
          <div className="h-80">
            <Plot
              data={[
                {
                  type: "scatter",
                  mode: "lines",
                  x: data?.timeseries.map((p) => p.date) ?? [],
                  y: data?.timeseries.map((p) => p.revenue) ?? [],
                  line: { color: "#3b82f6", width: 2 },
                  name: "Выручка",
                },
              ]}
              layout={plotLayout({ ymetric: "Выручка, ₽" })}
            />
          </div>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <h2 className="mb-3 text-sm font-medium uppercase tracking-wide text-slate-400">
            Доля категорий
          </h2>
          <div className="h-80">
            <Plot
              data={[
                {
                  type: "pie",
                  labels: data?.categoryShare.map((c) => c.category) ?? [],
                  values: data?.categoryShare.map((c) => c.revenue) ?? [],
                  hole: 0.45,
                  textinfo: "label+percent",
                  marker: { line: { color: "#0f172a", width: 1 } },
                },
              ]}
              layout={plotLayout({})}
            />
          </div>
        </div>
      </section>

      <section className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <h2 className="mb-3 text-sm font-medium uppercase tracking-wide text-slate-400">
            Регион × Категория (тепловая карта)
          </h2>
          <div className="h-80">
            <Plot
              data={[heatmapTrace(data?.regionCategory ?? [])]}
              layout={plotLayout({})}
            />
          </div>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-4">
          <h2 className="mb-3 text-sm font-medium uppercase tracking-wide text-slate-400">
            Топ-10 товаров по выручке
          </h2>
          <div className="h-80">
            <Plot
              data={[
                {
                  type: "bar",
                  orientation: "h",
                  x: (data?.topProducts ?? []).map((p) => p.revenue).reverse(),
                  y: (data?.topProducts ?? []).map((p) => p.name).reverse(),
                  marker: { color: "#22c55e" },
                  text: (data?.topProducts ?? [])
                    .map((p) => fmtRub(p.revenue))
                    .reverse(),
                  textposition: "auto",
                },
              ]}
              layout={plotLayout({ margins: { l: 200 } })}
            />
          </div>
        </div>
      </section>

      <section className="rounded-xl border border-slate-800 bg-slate-900">
        <h2 className="border-b border-slate-800 px-5 py-3 text-sm font-medium uppercase tracking-wide text-slate-400">
          Топ-10 товаров — таблица
        </h2>
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-800 text-left text-slate-400">
              <th className="px-5 py-2 font-medium">Товар</th>
              <th className="px-5 py-2 font-medium">Категория</th>
              <th className="px-5 py-2 text-right font-medium">Шт.</th>
              <th className="px-5 py-2 text-right font-medium">Выручка</th>
            </tr>
          </thead>
          <tbody>
            {(data?.topProducts ?? []).map((p) => (
              <tr
                key={p.productId}
                className="border-b border-slate-800/40 last:border-b-0"
              >
                <td className="px-5 py-2 text-white">{p.name}</td>
                <td className="px-5 py-2 text-slate-300">{p.category}</td>
                <td className="px-5 py-2 text-right text-slate-300">
                  {fmtInt(p.units)}
                </td>
                <td className="px-5 py-2 text-right text-white">
                  {fmtRub(p.revenue)}
                </td>
              </tr>
            ))}
            {!isLoading && (data?.topProducts.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-5 py-6 text-center text-slate-500">
                  Нет данных за выбранный период
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </section>
    </div>
  );
}

function heatmapTrace(points: RegionCategoryPoint[]) {
  const regions = Array.from(new Set(points.map((p) => p.region))).sort();
  const categories = Array.from(new Set(points.map((p) => p.category))).sort();
  const z = categories.map((cat) =>
    regions.map((reg) =>
      points.find((p) => p.region === reg && p.category === cat)?.revenue ?? 0,
    ),
  );
  return {
    type: "heatmap" as const,
    x: regions,
    y: categories,
    z,
    colorscale: "Blues" as const,
    hovertemplate: "%{y} — %{x}<br>Выручка: %{z:,.0f} ₽<extra></extra>",
  };
}

type LayoutOpts = { ymetric?: string; margins?: { l?: number } };

function plotLayout({ ymetric, margins }: LayoutOpts) {
  return {
    autosize: true,
    paper_bgcolor: "rgba(0,0,0,0)",
    plot_bgcolor: "rgba(0,0,0,0)",
    font: { color: "#cbd5e1", family: "system-ui, sans-serif" },
    margin: { t: 10, r: 10, b: 40, l: margins?.l ?? 50 },
    xaxis: { gridcolor: "#1e293b", zerolinecolor: "#1e293b" },
    yaxis: {
      gridcolor: "#1e293b",
      zerolinecolor: "#1e293b",
      title: ymetric ? { text: ymetric } : undefined,
    },
    legend: { orientation: "h" as const },
  };
}
