import Link from "next/link";

export default function NotFound() {
  return (
    <div className="mx-auto mt-20 max-w-xl rounded-xl border border-slate-800 bg-slate-900 p-8 text-center">
      <p className="text-sm uppercase tracking-wide text-slate-400">404</p>
      <h1 className="mt-2 text-2xl font-semibold text-white">Страница не найдена</h1>
      <p className="mt-3 text-sm text-slate-300">
        Запрошенный адрес не существует или был удалён.
      </p>
      <div className="mt-6">
        <Link
          href="/dashboard"
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500"
        >
          На дашборд
        </Link>
      </div>
    </div>
  );
}
