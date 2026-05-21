"use client";

import dynamic from "next/dynamic";
import type { PlotParams } from "react-plotly.js";

const PlotComponent = dynamic(() => import("react-plotly.js"), {
  ssr: false,
  loading: () => (
    <div className="flex h-72 items-center justify-center text-slate-500">
      Загрузка графика...
    </div>
  ),
});

export function Plot(props: PlotParams) {
  return (
    <PlotComponent
      useResizeHandler
      style={{ width: "100%", height: "100%" }}
      {...props}
      config={{ displaylogo: false, responsive: true, ...(props.config ?? {}) }}
    />
  );
}
