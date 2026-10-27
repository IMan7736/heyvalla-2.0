import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { makeOption } from "../test/fixtures";
import { renderIntl } from "../test/render";
import { ExplanationList } from "./explanation-list";
import { OptionCard } from "./option-card";
import { usePresenters } from "./use-presenters";

describe("OptionCard", () => {
  it("shows times, price, labels, risk and a plain-language why", () => {
    renderIntl(
      <OptionCard
        option={makeOption()}
        query="from=x"
        recommended
        comparing={false}
        compareDisabled={false}
        onToggleCompare={() => {}}
      />,
    );
    expect(
      screen.getByRole("article", { name: "Trip arriving 13:00, MVR 1,940" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("article")).toHaveTextContent("13:00");
    expect(screen.getByText("MVR 1,940")).toBeInTheDocument();
    expect(screen.getByText("Safest")).toBeInTheDocument();
    expect(screen.getByText("Recommended")).toBeInTheDocument();
    expect(screen.getByText("Low risk")).toBeInTheDocument();
    expect(screen.getByText(/150 minutes to spare/)).toBeInTheDocument();
    expect(screen.getByText("Includes estimated fares")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /View trip/ })).toHaveAttribute(
      "href",
      "/trip/trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2?from=x",
    );
  });

  it("toggles compare and disables it when the limit is reached", async () => {
    const toggle = vi.fn();
    const first = renderIntl(
      <OptionCard
        option={makeOption()}
        query=""
        recommended={false}
        comparing={false}
        compareDisabled={false}
        onToggleCompare={toggle}
      />,
    );
    await userEvent.click(screen.getByRole("checkbox", { name: /^Compare 07:30/ }));
    expect(toggle).toHaveBeenCalledWith("trp_01K8Z3M5Q7R9T2V4W6X8Y0E1F2");
    first.unmount();
    renderIntl(
      <OptionCard
        option={makeOption()}
        query=""
        recommended={false}
        comparing={false}
        compareDisabled
        onToggleCompare={toggle}
      />,
    );
    expect(screen.getByRole("checkbox", { name: /^Compare 07:30/ })).toBeDisabled();
  });
});

describe("ExplanationList", () => {
  it("degrades unknown codes to a generic note and logs once", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    renderIntl(
      <ExplanationList explanations={[{ code: "BRAND_NEW_CODE", params: {}, severity: "info" }]} />,
    );
    expect(screen.getByText("Additional note from the planner.")).toBeInTheDocument();
    expect(warn).toHaveBeenCalledTimes(1);
    warn.mockRestore();
  });

  it("applies ICU plurals", () => {
    renderIntl(
      <ExplanationList
        explanations={[
          { code: "BUFFER_TIGHT", params: { minutes: 1 }, severity: "caution" },
          { code: "FEWER_TRANSFERS", params: { count: 2 }, severity: "good" },
        ]}
      />,
    );
    expect(screen.getByText(/Only 1 minute to spare/)).toBeInTheDocument();
    expect(screen.getByText("Only 2 changes.")).toBeInTheDocument();
  });

  it("translates mode params", () => {
    renderIntl(
      <ExplanationList
        explanations={[
          { code: "LAST_SERVICE_OF_DAY", params: { mode: "ferry" }, severity: "caution" },
        ]}
      />,
    );
    expect(screen.getByText(/last Ferry of the day/)).toBeInTheDocument();
  });
});

function Probe() {
  const present = usePresenters();
  const option = makeOption();
  const ribbon = present.ribbon(option);
  const risk = present.risk(option, "bar");
  const buffer = present.bufferMeter({
    afterLegIndex: 0,
    minutes: 93,
    requiredMinutes: 90,
    comfortMinutes: 120,
  });
  return (
    <ul>
      <li data-testid="ribbon-label">{ribbon.label}</li>
      <li data-testid="leg">{ribbon.legs[0]?.description}</li>
      <li data-testid="buffer-level">{ribbon.buffers?.[0]?.level}</li>
      <li data-testid="risk">
        {risk.levelLabel}: {risk.valueText}
      </li>
      <li data-testid="meter">{buffer.valueText}</li>
    </ul>
  );
}

describe("presenters map contract data to design-system props in words", () => {
  it("describes the journey, risk and buffer without relying on colour", () => {
    renderIntl(<Probe />);
    expect(screen.getByTestId("ribbon-label")).toHaveTextContent("Journey: 2 legs");
    expect(screen.getByTestId("leg")).toHaveTextContent(
      /Ferry from Maafushi to Hulhumalé Ferry Terminal, 1h 30m/,
    );
    expect(screen.getByTestId("buffer-level")).toHaveTextContent("comfortable");
    expect(screen.getByTestId("risk")).toHaveTextContent(
      "Low risk: About 3% chance of missing a connection",
    );
    expect(screen.getByTestId("meter")).toHaveTextContent("93 min, Tight");
  });
});
