import { handleChzzkEntered } from "./handlers/handleChzzkEntered";
import { handleChzzkLeft } from "./handlers/handleChzzkLeft";
import { handleChzzkNavigated } from "./handlers/handleChzzkNavigated";
import type Socket from "./socket";
import type { ChzzkEvent } from "./types";
import { isChzzkUrl } from "./url";

const STORAGE_KEY = "tabUrls";
const tabUrls = new Map<number, string>();

function saveTabUrls(): void {
    const value = Object.fromEntries(
        Array.from(tabUrls, ([tabId, url]) => [String(tabId), url]),
    );

    void chrome.storage.session.set({ [STORAGE_KEY]: value }).catch(() => {});
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

function handleUrlChange(
    socket: Socket,
    tabId: number,
    url: string,
): void {
    const previousUrl = tabUrls.get(tabId);

    if (previousUrl === url) {
        return;
    }

    const wasOnChzzk = isChzzkUrl(previousUrl);
    const isOnChzzk = isChzzkUrl(url);

    if (!wasOnChzzk && !isOnChzzk) {
        return;
    }

    if (isOnChzzk) {
        tabUrls.set(tabId, url);
    } else {
        tabUrls.delete(tabId);
    }

    saveTabUrls();

    const event: ChzzkEvent = {
        tabId,
        url,
        previousUrl,
        reason: "navigation",
        socket,
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

function handleTabClosed(socket: Socket, tabId: number): void {
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
        socket,
    });
}

async function initialize(socket: Socket): Promise<void> {
    await chrome.action.enable();
    await restoreTabUrls();

    const tabs = await chrome.tabs.query({});

    for (const tab of tabs) {
        if (tab.id === undefined || !tab.url) {
            continue;
        }

        handleUrlChange(socket, tab.id, tab.url);
    }
}

export function register(socket: Socket): void {
    const ready = Promise.resolve()
        .then(() => initialize(socket))
        .catch(() => {});

    const afterReady = (callback: () => void): void => {
        void ready.then(callback).catch(() => {});
    };

    chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
        if (changeInfo.url) {
            afterReady(() =>
                handleUrlChange(socket, tabId, changeInfo.url!)
            );
        }
    });

    chrome.webNavigation.onCommitted.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() =>
                handleUrlChange(socket, details.tabId, details.url)
            );
        }
    });

    chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() =>
                handleUrlChange(socket, details.tabId, details.url)
            );
        }
    });

    chrome.webNavigation.onReferenceFragmentUpdated.addListener((details) => {
        if (details.frameId === 0) {
            afterReady(() =>
                handleUrlChange(socket, details.tabId, details.url)
            );
        }
    });

    chrome.tabs.onRemoved.addListener((tabId) => {
        afterReady(() => handleTabClosed(socket, tabId));
    });
}
