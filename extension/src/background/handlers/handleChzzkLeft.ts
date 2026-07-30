import type { ChzzkEvent } from "../types";

export function handleChzzkLeft(event: ChzzkEvent): void {
    if (event.reason === "navigation") {
        void chrome.action.disable(event.tabId).catch(() => {});
    }
}
