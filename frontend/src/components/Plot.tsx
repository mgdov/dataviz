"use client";

import dynamic from "next/dynamic";
import type { PlotParams } from "react-plotly.js";

/**
 * Plotly загружаем динамически (без SSR) — это тяжёлая клиентская библиотека
 * с зависимостью на window. Иначе Next.js при сборке ругается на отсутствие DOM.
 */
const PlotComponent = dynamic(() => import("react-plotly.js"), {
  ssr: false,
  loading: () => (
    <div
      role="status"
      aria-live="polite"
      className="flex h-full min-h-[16rem] items-center justify-center text-slate-500"
    >
      Загрузка графика…
    </div>
  ),
});

export function Plot(props: PlotParams) {
  return (
    <PlotComponent
      useResizeHandler
      style={{ width: "100%", height: "100%" }}
      {...props}
      config={{
        displaylogo: false,
        responsive: true,
        modeBarButtonsToRemove: ["lasso2d", "select2d"],
        ...(props.config ?? {}),
      }}
    />
  );
}
