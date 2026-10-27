import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useState } from "react";
import { describe, expect, it, vi } from "vitest";
import { BufferMeter } from "./BufferMeter";
import { Button } from "./Button";
import { Checkbox } from "./Checkbox";
import { Combobox } from "./Combobox";
import { DatePicker } from "./DatePicker";
import { Dialog } from "./Dialog";
import { Field } from "./Field";
import { IconButton } from "./IconButton";
import { Input } from "./Input";
import { JourneyRibbon } from "./JourneyRibbon";
import { NumberStepper } from "./NumberStepper";
import { RiskGauge } from "./RiskGauge";
import { Stepper } from "./Misc";
import { TripResultCard } from "./TripResultCard";

describe("Button", () => {
  it("fires onClick and is disabled while loading", async () => {
    const onClick = vi.fn();
    const { rerender } = render(<Button onClick={onClick}>Search</Button>);
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(onClick).toHaveBeenCalledOnce();
    rerender(
      <Button onClick={onClick} loading>
        Search
      </Button>,
    );
    expect(screen.getByRole("button", { name: "Search" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Search" })).toHaveAttribute("aria-busy", "true");
  });

  it("renders as the child element with asChild", () => {
    render(
      <Button asChild>
        <a href="/trips">My trips</a>
      </Button>,
    );
    expect(screen.getByRole("link", { name: "My trips" })).toHaveAttribute("href", "/trips");
  });
});

describe("IconButton", () => {
  it("exposes its label as the accessible name", () => {
    render(<IconButton icon="x" label="Close" />);
    expect(screen.getByRole("button", { name: "Close" })).toBeInTheDocument();
  });
});

describe("Field + Input", () => {
  it("wires label, hint and error to the control", () => {
    render(
      <Field
        label="Email"
        hint="We never share it"
        error="Enter a valid email"
        required
        requiredLabel="required"
      >
        <Input />
      </Field>,
    );
    const input = screen.getByLabelText(/Email/);
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(input).toHaveAccessibleDescription("Enter a valid email");
    expect(input).toHaveAttribute("aria-required", "true");
    expect(screen.getByText("(required)")).toBeInTheDocument();
  });
});

describe("Checkbox", () => {
  it("toggles by label click", async () => {
    const onChange = vi.fn();
    render(<Checkbox label="Remember me" onCheckedChange={onChange} />);
    await userEvent.click(screen.getByText("Remember me"));
    expect(onChange).toHaveBeenCalledWith(true);
  });
});

describe("Combobox", () => {
  const options = [
    { value: "isl_1", label: "Maafushi", description: "Kaafu Atoll" },
    { value: "isl_2", label: "Malé", description: "Kaafu Atoll" },
    { value: "apt_1", label: "Velana International Airport" },
  ];
  function Harness({ onPick }: { onPick: (v: string | null) => void }) {
    const [value, setValue] = useState<string | null>(null);
    return (
      <Combobox
        options={options}
        value={value}
        onValueChange={(v) => {
          setValue(v);
          onPick(v);
        }}
        placeholder="From"
        emptyLabel="No places found"
        resultsLabel={(n) => `${n} results`}
      />
    );
  }

  it("filters, navigates with the keyboard and selects with Enter", async () => {
    const onPick = vi.fn();
    render(<Harness onPick={onPick} />);
    const input = screen.getByRole("combobox");
    await userEvent.click(input);
    expect(await screen.findAllByRole("option")).toHaveLength(3);
    await userEvent.type(input, "ma");
    expect(screen.getAllByRole("option")).toHaveLength(2);
    await userEvent.keyboard("{ArrowDown}{Enter}");
    expect(onPick).toHaveBeenLastCalledWith("isl_2");
    expect(input).toHaveValue("Malé");
  });

  it("shows the empty message when nothing matches", async () => {
    render(<Harness onPick={() => {}} />);
    await userEvent.type(screen.getByRole("combobox"), "zzz");
    expect(await screen.findByText("No places found")).toBeInTheDocument();
  });

  it("closes on Escape", async () => {
    render(<Harness onPick={() => {}} />);
    const input = screen.getByRole("combobox");
    await userEvent.click(input);
    await screen.findAllByRole("option");
    await userEvent.keyboard("{Escape}");
    expect(screen.queryAllByRole("option")).toHaveLength(0);
  });
});

describe("DatePicker", () => {
  it("selects a date and reports it as YYYY-MM-DD", async () => {
    const onChange = vi.fn();
    render(
      <DatePicker
        value="2026-10-27"
        onValueChange={onChange}
        placeholder="Pick a date"
        labels={{ dialog: "Choose date", previousMonth: "Previous month", nextMonth: "Next month" }}
      />,
    );
    await userEvent.click(screen.getByRole("button", { name: /27 Oct 2026/ }));
    const dialog = await screen.findByLabelText("Choose date");
    await userEvent.click(within(dialog).getByRole("button", { name: /October 28th, 2026/ }));
    expect(onChange).toHaveBeenCalledWith("2026-10-28");
  }, 30000);
});

describe("NumberStepper", () => {
  it("respects min and max", async () => {
    const onChange = vi.fn();
    render(
      <NumberStepper
        value={1}
        min={1}
        max={2}
        onValueChange={onChange}
        label="Passengers"
        decrementLabel="Fewer"
        incrementLabel="More"
      />,
    );
    expect(screen.getByRole("button", { name: "Fewer" })).toBeDisabled();
    await userEvent.click(screen.getByRole("button", { name: "More" }));
    expect(onChange).toHaveBeenCalledWith(2);
  });
});

describe("Dialog", () => {
  it("is labelled, traps focus on open and closes with the close button", async () => {
    const onOpenChange = vi.fn();
    render(
      <Dialog
        open
        onOpenChange={onOpenChange}
        title="Cancel booking"
        description="This cannot be undone"
        closeLabel="Close"
      >
        <p>Body</p>
      </Dialog>,
    );
    const dialog = await screen.findByRole("dialog", { name: "Cancel booking" });
    expect(dialog).toHaveAccessibleDescription("This cannot be undone");
    await userEvent.click(screen.getByRole("button", { name: "Close" }));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });
});

describe("Stepper", () => {
  it("marks the current step and announces progress", () => {
    render(
      <Stepper
        steps={[
          { id: "a", label: "Travellers" },
          { id: "b", label: "Payment" },
          { id: "c", label: "Tickets" },
        ]}
        currentId="b"
        label="Booking progress"
        completeLabel="completed"
        currentLabel="current step"
      />,
    );
    const items = within(screen.getByRole("list", { name: "Booking progress" })).getAllByRole(
      "listitem",
    );
    expect(items[1]).toHaveAttribute("aria-current", "step");
    expect(items[0]).toHaveTextContent("completed");
  });
});

describe("Data visualisation", () => {
  it("BufferMeter exposes a meter with a text equivalent", () => {
    render(
      <BufferMeter
        minutes={35}
        requiredMinutes={20}
        comfortMinutes={45}
        label="Connection buffer"
        valueText="35 min, adequate"
      />,
    );
    const meter = screen.getByRole("meter", { name: "Connection buffer" });
    expect(meter).toHaveAttribute("aria-valuenow", "35");
    expect(meter).toHaveAttribute("aria-valuetext", "35 min, adequate");
  });

  it("RiskGauge reports percent and level in words", () => {
    render(
      <RiskGauge
        missProbability={0.02}
        level="low"
        label="Risk of missing a connection"
        levelLabel="Low risk"
        valueText="2%"
      />,
    );
    const meter = screen.getByRole("meter", { name: "Risk of missing a connection" });
    expect(meter).toHaveAttribute("aria-valuenow", "2");
    expect(meter).toHaveAttribute("aria-valuetext", "Low risk, 2%");
    expect(screen.getByText("Low risk")).toBeVisible();
  });
});

describe("JourneyRibbon + TripResultCard", () => {
  const ribbon = {
    label: "Journey: 2 legs",
    legs: [
      {
        id: "l1",
        mode: "ferry" as const,
        durationMin: 45,
        durationLabel: "45 min",
        description: "Ferry from Maafushi to Malé, 45 min",
        fromLabel: "Maafushi",
      },
      {
        id: "l2",
        mode: "taxi" as const,
        durationMin: 10,
        durationLabel: "10 min",
        description: "Taxi from Malé jetty to Velana, 10 min",
      },
    ],
    buffers: [
      {
        afterLegIndex: 0,
        level: "comfortable" as const,
        label: "35 min",
        levelLabel: "comfortable",
      },
    ],
  };

  it("lists legs with accessible descriptions and buffer level text", () => {
    render(<JourneyRibbon {...ribbon} />);
    const list = screen.getByRole("list", { name: "Journey: 2 legs" });
    expect(within(list).getByText("Ferry from Maafushi to Malé, 45 min")).toBeInTheDocument();
    expect(within(list).getByText(": comfortable")).toBeInTheDocument();
  });

  it("renders the boarding-pass card with price, labels and explanations", () => {
    render(
      <TripResultCard
        label="Trip arriving 16:40"
        departTime="13:05"
        arriveTime="16:40"
        arriveDay="Tue 27 Oct"
        durationLabel="3 h 35 min"
        transfersLabel="1 transfer"
        price="MVR 450"
        priceNote="for 2 passengers"
        labels={[{ id: "safest", label: "Safest", tone: "success" }]}
        ribbon={ribbon}
        risk={{
          missProbability: 0.02,
          level: "low",
          label: "Risk of missing a connection",
          levelLabel: "Low risk",
          valueText: "2%",
        }}
        explanations={[{ id: "e1", text: "35 min buffer at the jetty", severity: "good" }]}
        explanationsTitle="Why this trip"
      />,
    );
    const card = screen.getByRole("article", { name: "Trip arriving 16:40" });
    expect(within(card).getByText("MVR 450")).toBeInTheDocument();
    expect(within(card).getByText("Safest")).toBeInTheDocument();
    expect(within(card).getByText("35 min buffer at the jetty")).toBeInTheDocument();
  });
});
