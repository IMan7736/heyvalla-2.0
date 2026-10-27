import { cleanup, fireEvent, render } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";

afterEach(() => cleanup());
import { detectMac, isTypingTarget, matchesCombo, SHORTCUTS, useShortcut } from "./shortcuts";

const key = (key: string, extra: Partial<KeyboardEvent> = {}) => ({
  key,
  ctrlKey: false,
  metaKey: false,
  altKey: false,
  shiftKey: false,
  ...extra,
});

describe("matchesCombo", () => {
  it("maps mod to Cmd on Mac and Ctrl elsewhere", () => {
    expect(matchesCombo(key("k", { metaKey: true }), "mod+k", true)).toBe(true);
    expect(matchesCombo(key("k", { ctrlKey: true }), "mod+k", true)).toBe(false);
    expect(matchesCombo(key("k", { ctrlKey: true }), "mod+k", false)).toBe(true);
  });

  it("does not fire plain keys while a modifier is held", () => {
    expect(matchesCombo(key("?"), "?", false)).toBe(true);
    expect(matchesCombo(key("?", { ctrlKey: true }), "?", false)).toBe(false);
  });

  it("detects Mac platforms", () => {
    expect(detectMac("MacIntel")).toBe(true);
    expect(detectMac("Linux x86_64")).toBe(false);
  });
});

describe("isTypingTarget", () => {
  it("is true for form fields and false for buttons", () => {
    expect(isTypingTarget(document.createElement("input"))).toBe(true);
    expect(isTypingTarget(document.createElement("textarea"))).toBe(true);
    expect(isTypingTarget(document.createElement("button"))).toBe(false);
    expect(isTypingTarget(null)).toBe(false);
  });
});

describe("SHORTCUTS", () => {
  it("has unique ids and key combos", () => {
    expect(new Set(SHORTCUTS.map((s) => s.id)).size).toBe(SHORTCUTS.length);
    expect(new Set(SHORTCUTS.map((s) => s.keys)).size).toBe(SHORTCUTS.length);
  });
});

function Harness({ keys, onFire }: { keys: string; onFire: () => void }) {
  useShortcut(keys, onFire);
  return <input aria-label="field" />;
}

describe("useShortcut", () => {
  it("fires a single key and ignores it while typing", () => {
    const onFire = vi.fn();
    const { getByLabelText } = render(<Harness keys="?" onFire={onFire} />);
    fireEvent.keyDown(document.body, { key: "?" });
    expect(onFire).toHaveBeenCalledTimes(1);
    fireEvent.keyDown(getByLabelText("field"), { key: "?" });
    expect(onFire).toHaveBeenCalledTimes(1);
  });

  it("fires mod combos even inside inputs", () => {
    const onFire = vi.fn();
    const { getByLabelText } = render(<Harness keys="mod+k" onFire={onFire} />);
    fireEvent.keyDown(getByLabelText("field"), { key: "k", ctrlKey: true });
    expect(onFire).toHaveBeenCalledTimes(1);
  });

  it("completes a two-key sequence and resets on a wrong key", () => {
    const onFire = vi.fn();
    render(<Harness keys="g h" onFire={onFire} />);
    fireEvent.keyDown(document.body, { key: "g" });
    fireEvent.keyDown(document.body, { key: "x" });
    fireEvent.keyDown(document.body, { key: "h" });
    expect(onFire).not.toHaveBeenCalled();
    fireEvent.keyDown(document.body, { key: "g" });
    fireEvent.keyDown(document.body, { key: "h" });
    expect(onFire).toHaveBeenCalledTimes(1);
  });
});
