import { NextIntlClientProvider } from "next-intl";
import { StrictMode, useRef } from "react";
import { createRoot } from "react-dom/client";
import messages from "../../../../messages/en/map.json";
import { TripMap, type TripMapHandle } from "../components/trip-map";
import { useMapFocus } from "../hooks/use-map-focus";
import { MapPicker } from "../components/map-picker";
import { NetworkMap } from "../components/network-map";
import { useState } from "react";
import { sampleOption, samplePlaces } from "./sample-data";
import "./lab.css";

function TripLab() {
  const focus = useMapFocus();
  const handle = useRef<TripMapHandle>(null);
  const params = new URLSearchParams(location.search);
  return (
    <main className="stage">
      <TripMap
        option={sampleOption}
        focus={focus}
        ref={handle}
        autoPlay={params.get("autoplay") === "1"}
        showScrubber={params.get("scrubber") !== "0"}
      />
    </main>
  );
}

function NetworkLab() {
  const counts = Object.fromEntries(samplePlaces.map((p, i) => [p.id, (i * 7) % 40]));
  return (
    <main className="stage">
      <NetworkMap places={samplePlaces} serviceCounts={counts} />
    </main>
  );
}

function PickerLab() {
  const [value, setValue] = useState<string | null>(null);
  return (
    <main className="stage">
      <MapPicker
        places={samplePlaces}
        value={value}
        onChange={(p) => setValue(p?.id ?? null)}
        label="From"
      />
    </main>
  );
}

const view = new URLSearchParams(location.search).get("view") ?? "trip";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <NextIntlClientProvider locale="en" timeZone="Indian/Maldives" messages={{ map: messages }}>
      {view === "trip" ? <TripLab /> : view === "network" ? <NetworkLab /> : <PickerLab />}
    </NextIntlClientProvider>
  </StrictMode>,
);
