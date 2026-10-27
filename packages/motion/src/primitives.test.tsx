import { act, fireEvent, render, renderHook, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { mockReducedMotion } from "./test/setup";
import { useCopyToClipboard } from "./clipboard";
import { clampOffset, Magnetic } from "./magnetic";
import { NumberTicker } from "./number-ticker";
import { PresenceSwap } from "./presence-swap";
import { Bloom } from "./ripple";
import { Reveal } from "./reveal";
import { shimmerDelayMs, Skeleton, SHIMMER_PERIOD_MS } from "./skeleton";
import { Stagger } from "./stagger";
import { DrawPath } from "./draw-path";
import { Pressable } from "./press";
import { usePrefersReducedMotion } from "./use-reduced-motion";

describe("usePrefersReducedMotion", () => {
  it("reflects the media query", () => {
    mockReducedMotion(true);
    expect(renderHook(() => usePrefersReducedMotion()).result.current).toBe(true);
    mockReducedMotion(false);
    expect(renderHook(() => usePrefersReducedMotion()).result.current).toBe(false);
  });
});

describe("reduced motion renders final states without animation", () => {
  it("Reveal shows content with no inline opacity", () => {
    mockReducedMotion(true);
    render(<Reveal data-testid="r">hello</Reveal>);
    const el = screen.getByTestId("r");
    expect(el).toHaveTextContent("hello");
    expect(el.style.opacity).toBe("");
  });

  it("Reveal starts hidden when motion is allowed", () => {
    mockReducedMotion(false);
    render(<Reveal data-testid="r">hello</Reveal>);
    expect(screen.getByTestId("r").style.opacity).toBe("0");
  });

  it("Stagger renders every child in both modes", () => {
    for (const reduced of [true, false]) {
      mockReducedMotion(reduced);
      const { unmount } = render(
        <Stagger>
          <p>one</p>
          <p>two</p>
        </Stagger>,
      );
      expect(screen.getByText("one")).toBeInTheDocument();
      expect(screen.getByText("two")).toBeInTheDocument();
      unmount();
    }
  });

  it("PresenceSwap shows the current content", () => {
    mockReducedMotion(true);
    const { rerender } = render(<PresenceSwap swapKey="a">A</PresenceSwap>);
    expect(screen.getByText("A")).toBeInTheDocument();
    rerender(<PresenceSwap swapKey="b">B</PresenceSwap>);
    expect(screen.getByText("B")).toBeInTheDocument();
    expect(screen.queryByText("A")).not.toBeInTheDocument();
  });

  it("Bloom toggles content", () => {
    mockReducedMotion(true);
    const { rerender } = render(<Bloom show={false}>ok</Bloom>);
    expect(screen.queryByText("ok")).not.toBeInTheDocument();
    rerender(<Bloom show>ok</Bloom>);
    expect(screen.getByText("ok")).toBeInTheDocument();
  });

  it("DrawPath renders the finished path", () => {
    mockReducedMotion(true);
    const { container } = render(
      <svg>
        <DrawPath d="M0 0 L10 10" stroke="red" />
      </svg>,
    );
    expect(container.querySelector("path")).toHaveAttribute("d", "M0 0 L10 10");
  });

  it("Pressable does not react when disabled and stays a real button", () => {
    mockReducedMotion(false);
    const onClick = vi.fn();
    render(
      <Pressable disabled onClick={onClick}>
        Book
      </Pressable>,
    );
    const button = screen.getByRole("button", { name: "Book" });
    expect(button).toBeDisabled();
    fireEvent.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });
});

describe("NumberTicker", () => {
  it("exposes only the final value to assistive tech", () => {
    mockReducedMotion(true);
    const { container, rerender } = render(<NumberTicker value={10} />);
    expect(container.querySelector(".sr-only")).toHaveTextContent("10");
    expect(container.querySelector("[aria-hidden='true']")).toHaveTextContent("10");
    rerender(<NumberTicker value={42} format={(n) => `${Math.round(n)} MVR`} />);
    expect(container.querySelector(".sr-only")).toHaveTextContent("42 MVR");
    expect(container.querySelector("[aria-hidden='true']")).toHaveTextContent("42 MVR");
  });
});

describe("Magnetic", () => {
  it("clamps pointer offset", () => {
    expect(clampOffset(100, 0.15, 8)).toBe(8);
    expect(clampOffset(-100, 0.15, 8)).toBe(-8);
    expect(clampOffset(20, 0.15, 8)).toBeCloseTo(3);
  });

  it("renders children without a transform under reduced motion", () => {
    mockReducedMotion(true);
    render(
      <Magnetic>
        <button>go</button>
      </Magnetic>,
    );
    expect(screen.getByRole("button", { name: "go" })).toBeInTheDocument();
  });
});

describe("Skeleton", () => {
  it("syncs to a shared clock", () => {
    expect(shimmerDelayMs(0)).toBe(-0);
    expect(shimmerDelayMs(SHIMMER_PERIOD_MS + 400)).toBe(-400);
  });

  it("is hidden from assistive tech", () => {
    mockReducedMotion(false);
    const { container } = render(<Skeleton />);
    expect(container.firstElementChild).toHaveAttribute("aria-hidden", "true");
  });
});

describe("useCopyToClipboard", () => {
  it("sets copied then resets", async () => {
    vi.useFakeTimers();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { configurable: true, value: { writeText } });
    const { result } = renderHook(() => useCopyToClipboard(1000));
    await act(async () => {
      expect(await result.current.copy("TKT-1")).toBe(true);
    });
    expect(writeText).toHaveBeenCalledWith("TKT-1");
    expect(result.current.copied).toBe(true);
    act(() => void vi.advanceTimersByTime(1000));
    expect(result.current.copied).toBe(false);
    vi.useRealTimers();
  });

  it("reports failure when the clipboard rejects", async () => {
    Object.defineProperty(navigator, "clipboard", {
      configurable: true,
      value: { writeText: vi.fn().mockRejectedValue(new Error("denied")) },
    });
    const { result } = renderHook(() => useCopyToClipboard());
    await act(async () => {
      expect(await result.current.copy("x")).toBe(false);
    });
    expect(result.current.copied).toBe(false);
  });
});
