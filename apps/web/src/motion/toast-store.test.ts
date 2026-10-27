import { afterEach, describe, expect, it } from "vitest";
import {
  MAX_VISIBLE_TOASTS,
  clearToasts,
  defaultDuration,
  dismissToast,
  setToastSink,
  showToast,
  useToasts,
} from "./toast-store";
import { renderHook, act } from "@testing-library/react";

afterEach(() => clearToasts());

describe("toast store", () => {
  it("adds, defaults the tone and dismisses by id", () => {
    const { result } = renderHook(() => useToasts());
    let id = 0;
    act(() => {
      id = showToast({ title: "Saved" });
    });
    expect(result.current).toHaveLength(1);
    expect(result.current[0]).toMatchObject({ title: "Saved", tone: "info" });
    act(() => dismissToast(id));
    expect(result.current).toHaveLength(0);
  });

  it("keeps only the newest toasts", () => {
    const { result } = renderHook(() => useToasts());
    act(() => {
      for (let i = 0; i < MAX_VISIBLE_TOASTS + 2; i++) showToast({ title: `m${i}` });
    });
    expect(result.current).toHaveLength(MAX_VISIBLE_TOASTS);
    expect(result.current.at(-1)?.title).toBe(`m${MAX_VISIBLE_TOASTS + 1}`);
  });

  it("ignores dismissing an unknown id", () => {
    const { result } = renderHook(() => useToasts());
    act(() => {
      showToast({ title: "x" });
      dismissToast(99999);
    });
    expect(result.current).toHaveLength(1);
  });

  it("uses the UI kit's default durations", () => {
    expect(defaultDuration("info")).toBe(6000);
    expect(defaultDuration("danger")).toBe(10000);
  });

  it("hands toasts to a registered host instead of storing them, and flushes pending ones", () => {
    const { result } = renderHook(() => useToasts());
    act(() => void showToast({ title: "early" }));
    expect(result.current).toHaveLength(1);
    const received: string[] = [];
    let unregister = () => {};
    act(() => {
      unregister = setToastSink((t) => received.push(t.title));
    });
    expect(received).toEqual(["early"]);
    expect(result.current).toHaveLength(0);
    act(() => void showToast({ title: "later" }));
    expect(received).toEqual(["early", "later"]);
    expect(result.current).toHaveLength(0);
    unregister();
    act(() => void showToast({ title: "fallback" }));
    expect(result.current).toHaveLength(1);
  });
});
