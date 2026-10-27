"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import type { Route } from "../geometry/route";

export interface TimelineOptions {
  route: Route;
  /** Controlled time (epoch ms). Leave undefined for the hook to own it. */
  time?: number;
  onTimeChange?: (timeMs: number) => void;
  autoPlay?: boolean;
  /** How long a full playback of the journey takes on screen. */
  playDurationMs?: number;
}

export interface Timeline {
  timeMs: number;
  playing: boolean;
  play: () => void;
  pause: () => void;
  toggle: () => void;
  seek: (timeMs: number) => void;
}

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * Journey clock for the scrubber and the vehicle. Time is real journey time (epoch ms);
 * playback just advances it faster than real time.
 */
export function useTimeline({
  route,
  time,
  onTimeChange,
  autoPlay = false,
  playDurationMs = 16_000,
}: TimelineOptions): Timeline {
  const { startMs, endMs } = route;
  const routeKey = `${startMs}-${endMs}`;
  // Internal clock is keyed by the route so a new route restarts at its own start.
  const [clock, setClock] = useState({ key: routeKey, ms: startMs });
  const internal = clock.key === routeKey ? clock.ms : startMs;
  const [playing, setPlaying] = useState(autoPlay && endMs > startMs);
  const controlled = time !== undefined;
  const timeMs = clamp(controlled ? time : internal, startMs, Math.max(startMs, endMs));
  const latest = useRef({ timeMs, onTimeChange, controlled });

  useEffect(() => {
    latest.current = { timeMs, onTimeChange, controlled };
  });

  const seek = useCallback(
    (next: number) => {
      const value = clamp(next, startMs, Math.max(startMs, endMs));
      if (!latest.current.controlled) setClock({ key: routeKey, ms: value });
      latest.current.onTimeChange?.(value);
    },
    [startMs, endMs, routeKey],
  );

  useEffect(() => {
    if (!playing || endMs <= startMs) return;
    let frame = 0;
    let last = performance.now();
    const speed = (endMs - startMs) / playDurationMs;
    const tick = (now: number) => {
      const next = latest.current.timeMs + (now - last) * speed;
      last = now;
      if (next >= endMs) {
        seek(endMs);
        setPlaying(false);
        return;
      }
      seek(next);
      frame = requestAnimationFrame(tick);
    };
    frame = requestAnimationFrame(tick);
    return () => cancelAnimationFrame(frame);
  }, [playing, startMs, endMs, playDurationMs, seek]);

  const play = useCallback(() => {
    // Replay from the start when the journey already finished.
    if (latest.current.timeMs >= endMs) seek(startMs);
    setPlaying(true);
  }, [endMs, startMs, seek]);
  const pause = useCallback(() => setPlaying(false), []);
  const toggle = useCallback(() => (playing ? pause() : play()), [playing, play, pause]);

  return { timeMs, playing, play, pause, toggle, seek };
}
