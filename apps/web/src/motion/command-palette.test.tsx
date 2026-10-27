import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import messages from "../../messages/en/motion.json";
import { CommandPalette } from "./command-palette";
import { closePalette } from "./palette-store";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

beforeAll(() => {
  globalThis.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
  Element.prototype.scrollIntoView = () => undefined;
});

beforeEach(() => {
  window.localStorage.clear();
  push.mockReset();
});

afterEach(() => {
  act(() => closePalette());
  cleanup();
  vi.unstubAllGlobals();
});

function mount() {
  return render(
    <NextIntlClientProvider locale="en" messages={{ motion: messages }}>
      <CommandPalette />
    </NextIntlClientProvider>,
  );
}

const openWithShortcut = () => fireEvent.keyDown(document.body, { key: "k", ctrlKey: true });

describe("CommandPalette", () => {
  it("is closed until Ctrl+K, then focuses the search field and lists navigation", async () => {
    mount();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    openWithShortcut();
    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(screen.getByRole("combobox")).toHaveFocus();
    expect(screen.getByText("My trips")).toBeInTheDocument();
  });

  it("navigates and closes when a destination is chosen", async () => {
    mount();
    openWithShortcut();
    fireEvent.click(await screen.findByText("My trips"));
    expect(push).toHaveBeenCalledWith("/trips");
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
  });

  it("searches islands, goes to the planner and remembers the choice", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({
            items: [{ id: "isl_1", type: "island", name: { en: "Maafushi" }, approximate: true }],
          }),
        ),
      ),
    );
    mount();
    openWithShortcut();
    fireEvent.change(await screen.findByRole("combobox"), { target: { value: "maaf" } });
    const result = await screen.findByText("Maafushi");
    expect(screen.getByText(/Approximate location/)).toBeInTheDocument();
    fireEvent.click(result);
    expect(push).toHaveBeenCalledWith("/plan?to=isl_1");
    expect(window.localStorage.getItem("dhathuru:recents:v1")).toContain("Maafushi");
  });

  it("shows an alert when the search fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("", { status: 502 })));
    mount();
    openWithShortcut();
    fireEvent.change(await screen.findByRole("combobox"), { target: { value: "maaf" } });
    expect(await screen.findByRole("alert")).toHaveTextContent("Could not search islands");
  });

  it("shows recents when the query is empty", async () => {
    window.localStorage.setItem(
      "dhathuru:recents:v1",
      JSON.stringify([
        { kind: "place", id: "isl_9", label: "Hulhumalé", href: "/plan?to=isl_9", at: 1 },
      ]),
    );
    mount();
    openWithShortcut();
    expect(await screen.findByText("Hulhumalé")).toBeInTheDocument();
  });

  it("opens the shortcut list with ? and returns to search", async () => {
    mount();
    fireEvent.keyDown(document.body, { key: "?" });
    expect(await screen.findByRole("heading", { name: "Keyboard shortcuts" })).toBeInTheDocument();
    expect(screen.getByText("Open the command palette")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Back to search" }));
    expect(await screen.findByRole("combobox")).toBeInTheDocument();
  });

  it("supports go-to sequences without opening the palette", () => {
    mount();
    fireEvent.keyDown(document.body, { key: "g" });
    fireEvent.keyDown(document.body, { key: "t" });
    expect(push).toHaveBeenCalledWith("/trips");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
