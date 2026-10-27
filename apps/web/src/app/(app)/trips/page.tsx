import { getTranslations } from "next-intl/server";
import { TripsList } from "@/features/booking";

export async function generateMetadata() {
  const t = await getTranslations("booking.trips");
  return { title: t("pageTitle") };
}

export default function TripsPage() {
  return (
    <main id="main">
      <TripsList />
    </main>
  );
}
