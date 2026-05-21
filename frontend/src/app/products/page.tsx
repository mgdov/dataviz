"use client";

import useSWR from "swr";
import { AuthGuard } from "@/components/AuthGuard";
import { swrFetcher } from "@/lib/api";
import { Category, Product } from "@/lib/types";
import { useState } from "react";

const fmtRub = (n: number) =>
  new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(n) + " ₽";

export default function ProductsPage() {
  return (
    <AuthGuard>
      <ProductsInner />
    </AuthGuard>
  );
}

function ProductsInner() {
  const [categoryId, setCategoryId] = useState<number | "">("");
  const { data: categories } = useSWR<Category[]>(
    "/api/categories",
    swrFetcher,
  );
  const url =
    categoryId === "" ? "/api/products" : `/api/products?categoryId=${categoryId}`;
  const { data, isLoading } = useSWR<Product[]>(url, swrFetcher);

  return (
    <div className="space-y-6">
      <header className="flex items-end justify-between">
        <div>
          <h1 className="text-2xl font-semibold text-white">Товары</h1>
          <p className="text-sm text-slate-400">
            Каталог товаров с фильтрацией по категориям
          </p>
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
            className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
          >
            <option value="">Все категории</option>
            {categories?.map((c) => (
              <option key={c.id} value={c.id}>
                {c.name}
              </option>
            ))}
          </select>
        </div>
      </header>

      <div className="rounded-xl border border-slate-800 bg-slate-900">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-800 text-left text-slate-400">
              <th className="px-5 py-2 font-medium">Название</th>
              <th className="px-5 py-2 font-medium">Категория</th>
              <th className="px-5 py-2 font-medium">Регион</th>
              <th className="px-5 py-2 text-right font-medium">Остаток</th>
              <th className="px-5 py-2 text-right font-medium">Цена</th>
            </tr>
          </thead>
          <tbody>
            {(data ?? []).map((p) => (
              <tr
                key={p.id}
                className="border-b border-slate-800/40 last:border-b-0"
              >
                <td className="px-5 py-2 text-white">{p.name}</td>
                <td className="px-5 py-2 text-slate-300">{p.categoryName}</td>
                <td className="px-5 py-2 text-slate-300">{p.regionCode}</td>
                <td className="px-5 py-2 text-right text-slate-300">
                  {p.stockQuantity}
                </td>
                <td className="px-5 py-2 text-right text-white">
                  {fmtRub(p.price)}
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
                  Нет товаров
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
