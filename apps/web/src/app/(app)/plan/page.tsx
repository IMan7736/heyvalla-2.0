import { getTranslations } from "next-intl/server";
import { TripSearchForm } from "@/features/planner";

export async function generateMetadata() {
  const t = await getTranslations("planner.plan");
  return { title: t("pageTitle") };
}

export default function PlanPage() {
  return (
    <main id="main" className="mx-auto max-w-[960px] px-10 py-16">
      <TripSearchForm />
    </main>
  );
}
