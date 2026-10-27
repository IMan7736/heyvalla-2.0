import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { afterEach, describe, expect, it, vi } from "vitest";
import messages from "../../messages/en/motion.json";
import { clearToasts, showToast } from "./toast-store";
import { Toaster } from "./toaster";

afterEach(() => {
  act(() => clearToasts());
  cleanup();
  vi.useRealTimers();
});

function mount() {
  return render(
    <NextIntlClientProvider locale="en" messages={{ motion: messages }}>
      <Toaster />
    </NextIntlClientProvider>,
  );
}

describe("Toaster", () => {
  it("announces toasts in a labelled live region and can dismiss them", () => {
    mount();
    act(() => void showToast({ title: "Trip saved" }));
    expect(screen.getByText("Trip saved")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Notifications" })).toHaveAttribute(
      "aria-live",
      "polite",
    );
    fireEvent.click(screen.getByRole("button", { name: "Dismiss notification" }));
  });

  it("runs the action and removes the toast", () => {
    mount();
    const onClick = vi.fn();
    act(() => void showToast({ title: "Booking cancelled", action: { label: "Undo", onClick } }));
    fireEvent.click(screen.getByRole("button", { name: "Undo" }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("auto-dismisses after its duration", async () => {
    mount();
    act(() => void showToast({ title: "Copied", duration: 50 }));
    expect(screen.getByText("Copied")).toBeInTheDocument();
    await waitFor(() => expect(screen.queryByText("Copied")).not.toBeInTheDocument());
  });
});
