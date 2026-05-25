"use client";

import { useEffect } from "react";

/**
 * Глобальный error boundary для всех страниц приложения.
 * Срабатывает, если в любом компоненте под /app выкинется исключение во время рендера.
 */
export default function GlobalError({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    // Можно подключить логирование в Sentry/аналог; пока пишем в консоль.
    console.error("Application error:", error);
  }, [error]);

  return (
    <div className="mx-auto mt-20 max-w-xl rounded-xl border border-red-900 bg-red-950 p-8 text-red-100">
      <h1 className="text-xl font-semibold">Что-то пошло не так</h1>
      <p className="mt-3 text-sm text-red-200">
        {error.message || "Произошла непредвиденная ошибка."}
      </p>
      {error.digest && (
        <p className="mt-2 text-xs text-red-300">Код инцидента: {error.digest}</p>
      )}
      <div className="mt-6 flex gap-3">
        <button
          onClick={() => reset()}
          className="rounded-md bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-blue-500"
        >
          Попробовать снова
        </button>
        <a
          href="/"
          className="rounded-md border border-red-700 px-4 py-2 text-sm font-medium text-red-100 transition-colors hover:bg-red-900"
        >
          На главную
        </a>
      </div>
    </div>
  );
}
