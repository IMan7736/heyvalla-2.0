import { getTranslations } from "next-intl/server";
import { TripBookingDetail } from "@/features/booking";
import { isBookingId } from "@/features/booking/server/guards";
import { notFound } from "next/navigation";

export async function generateMetadata() {
  const t = await getTranslations("booking.detail");
  return { title: t("pageTitle") };
}

export default async function TripPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  if (!isBookingId(id)) notFound();
  return (
    <main id="main">
      <TripBookingDetail bookingId={id} />
    </main>
  );
}
