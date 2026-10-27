import { notFound } from "next/navigation";
import { getTranslations } from "next-intl/server";
import { TripDetail } from "@/features/planner";
import { createApiClient } from "@/lib/api-client";
import { groupFromParams } from "@/features/planner/lib/search-state";

export async function generateMetadata() {
  const t = await getTranslations("planner.detail");
  return { title: t("pageTitle") };
}

const OPTION_ID = /^trp_[0-9A-Za-z]{10,40}$/;

type SearchParams = Record<string, string | string[] | undefined>;

/** The search that produced this option travels in the query string, so "back" restores the results. */
function backHref(searchParams: SearchParams): string {
  const carried = new URLSearchParams();
  for (const [key, value] of Object.entries(searchParams)) {
    if (typeof value === "string") carried.set(key, value);
  }
  const qs = carried.toString();
  return qs ? `/results?${qs}` : "/plan";
}

export default async function TripPage({
  params,
  searchParams,
}: {
  params: Promise<{ id: string }>;
  searchParams: Promise<SearchParams>;
}) {
  const [{ id }, query] = await Promise.all([params, searchParams]);
  // A real 404 for ids the API does not know; any other upstream failure falls through to the in-page error state.
  if (!OPTION_ID.test(id)) notFound();
  const status = await createApiClient()
    .GET("/trips/options/{optionId}", { params: { path: { optionId: id } } })
    .then((r) => r.response.status)
    .catch(() => null);
  if (status === 404) notFound();
  return (
    <main id="main">
      <TripDetail id={id} backHref={backHref(query)} group={groupFromParams(query)} />
    </main>
  );
}
