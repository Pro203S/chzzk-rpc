import type { ChzzkEvent } from "../types";

export function handleChzzkEntered(event: ChzzkEvent): void {
    void chrome.action.enable(event.tabId).catch(() => {});
}
