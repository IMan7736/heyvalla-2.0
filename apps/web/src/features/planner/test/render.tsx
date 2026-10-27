import { cleanup, render } from "@testing-library/react";
import { afterEach } from "vitest";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import planner from "../../../../messages/en/planner.json";
import uiMessages from "../../../../messages/en/ui.json";

afterEach(cleanup);

export function renderIntl(element: ReactElement) {
  return render(
    <NextIntlClientProvider
      locale="en"
      timeZone="Indian/Maldives"
      messages={{ planner, ui: uiMessages }}
    >
      {element}
    </NextIntlClientProvider>,
  );
}
