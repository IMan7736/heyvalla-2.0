import type { Metadata } from "next";
import { notFound } from "next/navigation";
import { Gallery } from "./Gallery";

export const metadata: Metadata = { title: "UI", robots: { index: false, follow: false } };

/** Design-system gallery. Hidden in production unless NEXT_PUBLIC_ENABLE_UI_GALLERY=1. */
export default function UiGalleryPage() {
  if (process.env.NODE_ENV === "production" && process.env.NEXT_PUBLIC_ENABLE_UI_GALLERY !== "1") {
    notFound();
  }
  return <Gallery />;
}
