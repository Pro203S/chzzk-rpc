import { handleChzzkEntered } from "./handlers/handleChzzkEntered";
import { handleChzzkLeft } from "./handlers/handleChzzkLeft";
import { handleChzzkNavigated } from "./handlers/handleChzzkNavigated";
import type { ChzzkEvent } from "./types";
import { isChzzkUrl } from "./url";

const STORAGE_KEY = "tabUrls";
const tabUrls = new Map<number, string>();

function saveTabUrls(): void {
    const value = Object.fromEntries(
        Array.from(tabUrls, ([tabId, url]) => [String(tabId), url]),
    );

    void chrome.storage.session.set({ [STORAGE_KEY]: value });
}

async function restoreTabUrls(): Promise<void> {
    const storedValue = await chrome.storage.session.get(STORAGE_KEY);
    const storedTabUrls = storedValue[STORAGE_KEY] as
        Record<string, unknown> | undefined;

    if (!storedTabUrls) {
        return;
    }

    for (const [tabId, url] of Object.entries(storedTabUrls)) {
        const parsedTabId = Number(tabId);

        if (Number.isInteger(parsedTabId) && typeof url === "string") {
            tabUrls.set(parsedTabId, url);
        }
    }
}

function handleUrlChange(tabId: number, url: string): void {
    const previousUrl = tabUrls.get(tabId);

    if (previousUrl === url) {
        return;
    }

    tabUrls.set(tabId, url);
    saveTabUrls();

    const wasOnChzzk = isChzzkUrl(previousUrl);
    const isOnChzzk = isChzzkUrl(url);
    const event: ChzzkEvent = {
        tabId,
        url,
        previousUrl,
        reason: "navigation",
    };

    if (!wasOnChzzk && isOnChzzk) {
        handleChzzkEntered(event);
        return;
    }

    if (wasOnChzzk && !isOnChzzk) {
        handleChzzkLeft(event);
        return;
    }

    if (wasOnChzzk && isOnChzzk) {
        handleChzzkNavigated(event);
    }
}

function handleTabClosed(tabId: number): void {
    const previousUrl = tabUrls.get(tabId);

    tabUrls.delete(tabId);
    saveTabUrls();

    if (!previousUrl || !isChzzkUrl(previousUrl)) {
        return;
    }

    handleChzzkLeft({
        tabId,
        url: previousUrl,
        previousUrl,
        reason: "tab-closed",
    });
}

async function initialize(): Promise<void> {
    await chrome.action.disable();
    await restoreTabUrls();

    const tabs = await chrome.tabs.query({});

    for (const tab of tabs) {
        if (tab.id === undefined || !tab.url) {
            continue;
        }

        if (isChzzkUrl(tab.url)) {
            await chrome.action.enable(tab.id);
        }

        handleUrlChange(tab.id, tab.url);
    }
}

export function register(): void {
    const ready = Promise.resolve()
        .then(initialize)
        .catch((error: unknown) => {
            console.error("[Discheese] 초기화 실패", error);
        });

    const afterReady = (callback: () => void): void => {
        void ready.then(callback);
    };

    chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
        if (changeInfo.url) {
            afterReady(() => handleUrlChange(tabId, changeInfo.url!));
        }
    });

    chrome.webNavigation.onCommitted.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() => handleUrlChange(details.tabId, details.url));
        }
    });

    chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() => handleUrlChange(details.tabId, details.url));
        }
    });

    chrome.webNavigation.onReferenceFragmentUpdated.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() => handleUrlChange(details.tabId, details.url));
        }
    });

    chrome.tabs.onRemoved.addListener((tabId) => {
        afterReady(() => handleTabClosed(tabId));
    });
}
