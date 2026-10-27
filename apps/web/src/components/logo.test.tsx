import { render, screen } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import { describe, expect, it } from "vitest";
import common from "../../messages/en/common.json";
import { Logo } from "./logo";

describe("<Logo />", () => {
  it("renders the brand SVG with an accessible name", () => {
    render(
      <NextIntlClientProvider locale="en" messages={{ common }}>
        <Logo />
      </NextIntlClientProvider>,
    );
    const img = screen.getByRole("img", { name: "Dhathuru" });
    expect(img).toHaveAttribute("src", "/brand/logo.svg");
  });
});
