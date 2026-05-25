/**
 * Root-level <c>loading.tsx</c>: показывается, пока серверный/клиентский
 * чанк страницы догружается. Помогает с метриками LCP/CLS на медленных сетях.
 */
export default function Loading() {
  return (
    <div className="flex h-[60vh] items-center justify-center text-slate-400" role="status" aria-live="polite">
      <span className="inline-flex items-center gap-2">
        <span
          aria-hidden
          className="h-2 w-2 animate-pulse rounded-full bg-blue-500"
        />
        Загрузка...
      </span>
    </div>
  );
}
