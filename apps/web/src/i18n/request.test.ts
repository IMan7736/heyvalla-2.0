import { describe, expect, it } from "vitest";
import { loadMessages } from "./request";

describe("loadMessages", () => {
  it("exposes one namespace per message file", async () => {
    const messages = await loadMessages("en");
    expect(messages).toHaveProperty("common.appName", "Dhathuru");
    expect(messages).toHaveProperty("home.title");
  });
});
