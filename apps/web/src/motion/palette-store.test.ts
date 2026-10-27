import { afterEach, describe, expect, it } from "vitest";
import {
  closePalette,
  getPaletteState,
  openPalette,
  setPaletteView,
  togglePalette,
} from "./palette-store";

afterEach(() => closePalette());

describe("palette store", () => {
  it("opens, switches view, toggles and closes back to commands", () => {
    openPalette("help");
    expect(getPaletteState()).toEqual({ open: true, view: "help" });
    setPaletteView("commands");
    expect(getPaletteState().view).toBe("commands");
    togglePalette();
    expect(getPaletteState().open).toBe(false);
    togglePalette();
    expect(getPaletteState().open).toBe(true);
    closePalette();
    expect(getPaletteState()).toEqual({ open: false, view: "commands" });
  });
});
