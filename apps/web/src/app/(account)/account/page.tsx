import { getTranslations } from "next-intl/server";
import { AccountScreen } from "@/features/account/AccountScreen";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("account") };
}

export default function Page() {
  return <AccountScreen />;
}
