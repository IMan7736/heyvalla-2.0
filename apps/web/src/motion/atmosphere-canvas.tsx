"use client";

import { useEffect, useRef } from "react";
import { waveY } from "./atmosphere-policy";

const LAYERS = [
  { color: "8, 127, 140", alpha: 0.1 },
  { color: "8, 127, 140", alpha: 0.14 },
  { color: "242, 140, 120", alpha: 0.06 },
  { color: "255, 255, 255", alpha: 0.16 },
] as const;

/** Render scale: the effect is soft, so a quarter of the pixels is plenty. */
const SCALE = 0.5;
const FRAME_MS = 1000 / 30;

/**
 * Soft drifting water layers. Runs at ~30 fps on a half-resolution canvas, pauses when the tab is
 * hidden, and only ever draws (no layout reads in the frame loop).
 */
export default function AtmosphereCanvas() {
  const ref = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = ref.current;
    const ctx = canvas?.getContext("2d");
    if (!canvas || !ctx) return;

    let width = 0;
    let height = 0;
    const resize = () => {
      width = Math.max(1, Math.round(canvas.clientWidth * SCALE));
      height = Math.max(1, Math.round(canvas.clientHeight * SCALE));
      canvas.width = width;
      canvas.height = height;
    };
    resize();
    const observer = new ResizeObserver(resize);
    observer.observe(canvas);

    let raf = 0;
    let last = 0;
    const draw = (now: number) => {
      raf = requestAnimationFrame(draw);
      if (now - last < FRAME_MS) return;
      last = now;
      const t = now / 1000;
      ctx.clearRect(0, 0, width, height);
      LAYERS.forEach((layer, index) => {
        ctx.beginPath();
        ctx.moveTo(0, height);
        for (let px = 0; px <= width; px += 8) {
          ctx.lineTo(px, waveY(px / width, t, index) * height);
        }
        ctx.lineTo(width, height);
        ctx.closePath();
        ctx.fillStyle = `rgba(${layer.color}, ${layer.alpha})`;
        ctx.fill();
      });
    };

    const onVisibility = () => {
      cancelAnimationFrame(raf);
      if (!document.hidden) raf = requestAnimationFrame(draw);
    };
    document.addEventListener("visibilitychange", onVisibility);
    raf = requestAnimationFrame(draw);

    return () => {
      cancelAnimationFrame(raf);
      observer.disconnect();
      document.removeEventListener("visibilitychange", onVisibility);
    };
  }, []);

  return <canvas ref={ref} aria-hidden="true" className="absolute inset-0 size-full" />;
}
