import type { ChzzkEvent } from "../types";

export function handleChzzkEntered(event: ChzzkEvent): void {
    void chrome.action.enable(event.tabId);
    console.log("[Discheese] 치지직 진입", event);
}
