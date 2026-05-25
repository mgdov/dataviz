"use client";

import { FormEvent, useEffect, useState } from "react";
import useSWR, { useSWRConfig } from "swr";
import { useRouter } from "next/navigation";
import { api, swrFetcher, getUser } from "@/lib/api";
import { Category, Product } from "@/lib/types";

export default function AdminPage() {
  const router = useRouter();
  const user = getUser();

  useEffect(() => {
    if (!user) router.replace("/login");
    else if (user.role !== "admin") router.replace("/dashboard");
  }, [user, router]);

  return (
    <div className="mx-auto mt-8 max-w-7xl px-6">
      <h1 className="mb-4 text-2xl font-semibold text-white">Админ — Управление</h1>
      <AdminPanel />
    </div>
  );
}

function AdminPanel() {
  const { data: categories } = useSWR<Category[]>("/api/categories", swrFetcher);
  const { data: products } = useSWR<Product[]>("/api/products", swrFetcher);
  const { mutate } = useSWRConfig();

  const [catName, setCatName] = useState("");
  const [catDesc, setCatDesc] = useState("");
  const [prodName, setProdName] = useState("");
  const [prodDesc, setProdDesc] = useState("");
  const [prodPrice, setProdPrice] = useState<number | "">("");
  const [prodStock, setProdStock] = useState<number | "">("");
  const [prodRegion, setProdRegion] = useState("MSK");
  const [prodCategoryId, setProdCategoryId] = useState<number | "">("");
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (categories && categories.length > 0 && !prodCategoryId) setProdCategoryId(categories[0].id);
  }, [categories]);

  const createCategory = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/categories", {
        method: "POST",
        body: JSON.stringify({ name: catName, description: catDesc }),
      });
      setCatName("");
      setCatDesc("");
      await mutate("/api/categories");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const deleteCategory = async (id: number) => {
    if (!confirm("Удалить категорию?")) return;
    try {
      await api(`/api/categories/${id}`, { method: "DELETE" });
      await mutate("/api/categories");
      await mutate("/api/products");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const createProduct = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await api("/api/products", {
        method: "POST",
        body: JSON.stringify({
          name: prodName,
          description: prodDesc,
          price: Number(prodPrice) || 0,
          stockQuantity: Number(prodStock) || 0,
          regionCode: prodRegion,
          categoryId: Number(prodCategoryId),
        }),
      });
      setProdName("");
      setProdDesc("");
      setProdPrice("");
      setProdStock("");
      await mutate("/api/products");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const deleteProduct = async (id: number) => {
    if (!confirm("Удалить товар?")) return;
    try {
      await api(`/api/products/${id}`, { method: "DELETE" });
      await mutate("/api/products");
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <div className="rounded-xl border border-slate-800 bg-slate-900 p-6">
        <h2 className="mb-4 text-lg font-semibold text-white">Категории</h2>
        <form onSubmit={createCategory} className="space-y-3">
          <input value={catName} onChange={(e) => setCatName(e.target.value)} required placeholder="Название" className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
          <input value={catDesc} onChange={(e) => setCatDesc(e.target.value)} placeholder="Описание" className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
          <div className="flex gap-3">
            <button className="rounded-md bg-blue-600 px-4 py-2 text-white">Создать</button>
          </div>
        </form>

        <div className="mt-6">
          {(categories ?? []).map((c) => (
            <div key={c.id} className="flex items-center justify-between border-b border-slate-800 py-2">
              <div>
                <div className="text-white">{c.name}</div>
                <div className="text-slate-400 text-sm">{c.description}</div>
              </div>
              <div>
                <button onClick={() => deleteCategory(c.id)} className="rounded-md bg-red-600 px-3 py-1 text-white">Удалить</button>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="rounded-xl border border-slate-800 bg-slate-900 p-6">
        <h2 className="mb-4 text-lg font-semibold text-white">Товары</h2>
        <form onSubmit={createProduct} className="space-y-3">
          <input value={prodName} onChange={(e) => setProdName(e.target.value)} required placeholder="Название" className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
          <input value={prodDesc} onChange={(e) => setProdDesc(e.target.value)} placeholder="Описание" className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
          <div className="grid gap-3 md:grid-cols-3">
            <input value={prodPrice} onChange={(e) => setProdPrice(e.target.value === "" ? "" : Number(e.target.value))} placeholder="Цена" type="number" className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
            <input value={prodStock} onChange={(e) => setProdStock(e.target.value === "" ? "" : Number(e.target.value))} placeholder="Остаток" type="number" className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
            <input value={prodRegion} onChange={(e) => setProdRegion(e.target.value)} placeholder="Регион" className="rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white" />
          </div>
          <select value={prodCategoryId ?? ""} onChange={(e) => setProdCategoryId(Number(e.target.value))} className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white">
            <option value="">Выберите категорию</option>
            {(categories ?? []).map((c) => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
          <div className="flex gap-3">
            <button className="rounded-md bg-blue-600 px-4 py-2 text-white">Создать товар</button>
          </div>
        </form>

        <div className="mt-6">
          {(products ?? []).map((p) => (
            <div key={p.id} className="flex items-center justify-between border-b border-slate-800 py-2">
              <div>
                <div className="text-white">{p.name} — {p.categoryName}</div>
                <div className="text-slate-400 text-sm">{p.regionCode} • {p.stockQuantity} шт. • {p.price} ₽</div>
              </div>
              <div>
                <button onClick={() => deleteProduct(p.id)} className="rounded-md bg-red-600 px-3 py-1 text-white">Удалить</button>
              </div>
            </div>
          ))}
        </div>

        {error && <div className="mt-4 text-sm text-red-400">{error}</div>}
      </div>
    </div>
  );
}
