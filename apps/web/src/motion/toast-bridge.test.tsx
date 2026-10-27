import { ToastProvider } from "@dhathuru/ui";
import { act, cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { ToastBridge } from "./toast-bridge";
import { clearToasts, showToast } from "./toast-store";

afterEach(() => {
  clearToasts();
  cleanup();
});

describe("ToastBridge", () => {
  it("shows store toasts through the UI kit host with a single notifications region", async () => {
    render(
      <ToastProvider closeLabel="Dismiss" regionLabel="Notifications">
        <ToastBridge />
      </ToastProvider>,
    );
    act(() => void showToast({ title: "Booked", description: "See My trips", tone: "success" }));
    expect(await screen.findByText("Booked")).toBeInTheDocument();
    expect(screen.getByText("See My trips")).toBeInTheDocument();
    expect(screen.getAllByRole("region", { name: /Notifications/ })).toHaveLength(1);
  });
});
