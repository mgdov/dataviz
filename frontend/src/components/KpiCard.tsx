type Props = {
  title: string;
  value: string | number;
  hint?: string;
};

export function KpiCard({ title, value, hint }: Props) {
  return (
    <div
      className="rounded-xl border border-slate-800 bg-slate-900 p-5"
      role="group"
      aria-label={title}
    >
      <div className="text-xs uppercase tracking-wide text-slate-400">{title}</div>
      <div className="mt-2 text-2xl font-semibold text-white">{value}</div>
      {hint && <div className="mt-1 text-xs text-slate-500">{hint}</div>}
    </div>
  );
}
