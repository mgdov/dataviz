"use client";

import { FormEvent, useMemo, useState } from "react";
import useSWR, { useSWRConfig } from "swr";
import { AuthGuard } from "@/components/AuthGuard";
import { api, swrFetcher } from "@/lib/api";
import { Order, Product } from "@/lib/types";

const fmtRub = (n: number) =>
  new Intl.NumberFormat("ru-RU", { maximumFractionDigits: 0 }).format(n) + " ₽";

const fmtDate = (s: string) =>
  new Date(s).toLocaleString("ru-RU", { dateStyle: "short", timeStyle: "short" });

type OrderItemInput = {
  productId: number;
  quantity: number;
};

export default function OrdersPage() {
  return (
    <AuthGuard>
      <OrdersInner />
    </AuthGuard>
  );
}

function OrdersInner() {
  const { data, isLoading } = useSWR<Order[]>("/api/orders", swrFetcher);
  const { data: products } = useSWR<Product[]>("/api/products", swrFetcher);
  const { mutate } = useSWRConfig();
  const [regionCode, setRegionCode] = useState("MOW");
  const [items, setItems] = useState<OrderItemInput[]>([
    { productId: 0, quantity: 1 },
  ]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const availableProducts = useMemo(() => products ?? [], [products]);

  const defaultProductId = useMemo(
    () => availableProducts[0]?.id ?? 0,
    [availableProducts],
  );

  const orderItems = items.map((item) => ({
    ...item,
    productId: item.productId || defaultProductId,
  }));

  const handleChange = (
    index: number,
    field: keyof OrderItemInput,
    value: number,
  ) => {
    setItems((prev) =>
      prev.map((item, idx) =>
        idx !== index ? item : { ...item, [field]: value },
      ),
    );
  };

  const addItem = () => {
    setItems((prev) => [...prev, { productId: defaultProductId, quantity: 1 }]);
  };

  const removeItem = (index: number) => {
    setItems((prev) => prev.filter((_, idx) => idx !== index));
  };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    if (availableProducts.length === 0) {
      setError("Нет товаров для создания заказа.");
      return;
    }

    const payload = {
      regionCode: regionCode.trim(),
      items: orderItems
        .filter((item) => item.productId > 0 && item.quantity > 0)
        .map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
    };

    if (payload.items.length === 0) {
      setError("Добавьте хотя бы один товар и количество больше 0.");
      return;
    }

    try {
      setLoading(true);
      await api("/api/orders", {
        method: "POST",
        body: JSON.stringify(payload),
      });
      setItems([{ productId: defaultProductId, quantity: 1 }]);
      await mutate("/api/orders");
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-6">
      <header className="space-y-2">
        <div>
          <h1 className="text-2xl font-semibold text-white">Заказы</h1>
          <p className="text-sm text-slate-400">Заказы текущего пользователя</p>
        </div>
      </header>

      <div className="grid gap-6 lg:grid-cols-[1.2fr_0.8fr]">
        <div className="rounded-xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="mb-4 text-lg font-semibold text-white">Создать заказ</h2>
          <form onSubmit={submit} className="space-y-4">
            <div>
              <label className="mb-2 block text-sm text-slate-300">Регион</label>
              <input
                value={regionCode}
                onChange={(e) => setRegionCode(e.target.value)}
                required
                className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white focus:border-blue-500 focus:outline-none"
              />
            </div>

            <div className="space-y-3">
              {orderItems.map((item, index) => (
                <div key={index} className="grid gap-3 md:grid-cols-[1fr_90px_40px]">
                  <div>
                    <label className="mb-2 block text-sm text-slate-300">Товар</label>
                    <select
                      value={item.productId || defaultProductId}
                      onChange={(e) =>
                        handleChange(index, "productId", Number(e.target.value))
                      }
                      className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                    >
                      {availableProducts.map((product) => (
                        <option key={product.id} value={product.id}>
                          {product.name} — {fmtRub(product.price)} ({product.stockQuantity} в наличии)
                        </option>
                      ))}
                    </select>
                  </div>
                  <div>
                    <label className="mb-2 block text-sm text-slate-300">Кол-во</label>
                    <input
                      type="number"
                      min={1}
                      value={item.quantity}
                      onChange={(e) =>
                        handleChange(index, "quantity", Number(e.target.value))
                      }
                      className="w-full rounded-md border border-slate-700 bg-slate-950 px-3 py-2 text-white"
                    />
                  </div>
                  <button
                    type="button"
                    onClick={() => removeItem(index)}
                    className="mt-6 h-10 rounded-md bg-red-600 px-3 text-white hover:bg-red-500"
                  >
                    Удалить
                  </button>
                </div>
              ))}
            </div>

            <div className="flex items-center gap-3">
              <button
                type="button"
                onClick={addItem}
                className="rounded-md bg-slate-700 px-4 py-2 text-white hover:bg-slate-600"
              >
                Добавить позицию
              </button>
              <button
                type="submit"
                disabled={loading}
                className="rounded-md bg-blue-600 px-4 py-2 text-white hover:bg-blue-500 disabled:opacity-50"
              >
                {loading ? "Создаём..." : "Создать заказ"}
              </button>
            </div>

            {error && <div className="text-sm text-red-400">{error}</div>}
            {!products && (
              <div className="text-sm text-slate-400">Загружаем товары...</div>
            )}
          </form>
        </div>

        <div className="rounded-xl border border-slate-800 bg-slate-900 p-6">
          <h2 className="mb-4 text-lg font-semibold text-white">История заказов</h2>
          <div className="overflow-x-auto">
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
      </div>
    </div>
  );
}
