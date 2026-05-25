import Link from "next/link";

export const metadata = {
  title: "Нет доступа · DataViz",
};

export default function UnauthorizedPage() {
  return (
    <div className="mx-auto mt-20 max-w-xl rounded-xl border border-slate-800 bg-slate-900 p-8 text-center">
      <p className="text-sm uppercase tracking-wide text-slate-400">403</p>
      <h1 className="mt-2 text-2xl font-semibold text-white">Нет доступа</h1>
      <p className="mt-3 text-sm text-slate-300">
        Этот раздел доступен только администраторам.
      </p>
      <div className="mt-6 flex justify-center gap-3">
        <Link
          href="/dashboard"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500"
        >
          На дашборд
        </Link>
        <Link
          href="/login"
          className="rounded-md border border-slate-700 px-4 py-2 text-sm font-medium text-slate-200 transition-colors hover:bg-slate-800"
        >
          Сменить аккаунт
        </Link>
      </div>
    </div>
  );
}
