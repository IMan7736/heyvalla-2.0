import { cleanup, render } from "@testing-library/react";
import { NextIntlClientProvider } from "next-intl";
import type { ReactElement } from "react";
import { afterEach } from "vitest";
import booking from "../../../../messages/en/booking.json";
import planner from "../../../../messages/en/planner.json";
import uiMessages from "../../../../messages/en/ui.json";
import { BookingProviders } from "../components/booking-providers";

afterEach(cleanup);

export function renderBooking(element: ReactElement) {
  return render(
    <NextIntlClientProvider
      locale="en"
      timeZone="Indian/Maldives"
      messages={{ booking, planner, ui: uiMessages }}
    >
      <BookingProviders>{element}</BookingProviders>
    </NextIntlClientProvider>,
  );
}
