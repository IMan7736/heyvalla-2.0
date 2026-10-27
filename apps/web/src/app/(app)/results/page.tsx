import { getTranslations } from "next-intl/server";
import { ResultsView } from "@/features/planner";

export async function generateMetadata() {
  const t = await getTranslations("planner.results");
  return { title: t("pageTitle") };
}

export default function ResultsPage() {
  return (
    <main id="main">
      <ResultsView />
    </main>
  );
}
