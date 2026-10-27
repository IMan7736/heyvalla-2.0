import { getTranslations } from "next-intl/server";
import { ForgotPasswordScreen } from "@/features/account/screens";

export async function generateMetadata() {
  const t = await getTranslations("account.meta");
  return { title: t("forgot") };
}

export default function Page() {
  return <ForgotPasswordScreen />;
}
