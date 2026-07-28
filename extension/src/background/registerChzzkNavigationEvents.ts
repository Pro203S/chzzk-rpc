import { isChzzkUrl } from "./chzzkUrl";
import {
    handleChzzkEntered,
    handleChzzkLeft,
    handleChzzkNavigated,
    handleOutsideChzzk,
} from "./handlers/chzzkNavigationHandlers";

const tabUrls = new Map<number, string>();

function handleUrlChange(tabId: number, url: string): void {
    const previousUrl = tabUrls.get(tabId);

    if (previousUrl === url) {
        return;
    }

    tabUrls.set(tabId, url);

    const wasOnChzzk = isChzzkUrl(previousUrl);
    const isOnChzzk = isChzzkUrl(url);
    const event = { tabId, url, previousUrl };

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
        return;
    }

    handleOutsideChzzk(event);
}

async function initializeOpenTabs(): Promise<void> {
    await chrome.action.disable();

    const tabs = await chrome.tabs.query({});

    for (const tab of tabs) {
        if (tab.id !== undefined && tab.url) {
            handleUrlChange(tab.id, tab.url);
        }
    }
}

export function registerChzzkNavigationEvents(): void {
    chrome.tabs.onUpdated.addListener((tabId, changeInfo) => {
        if (changeInfo.url) {
            handleUrlChange(tabId, changeInfo.url);
        }
    });

    chrome.webNavigation.onCommitted.addListener((details) => {
        if (details.frameId === 0) {
            handleUrlChange(details.tabId, details.url);
        }
    });

    chrome.webNavigation.onHistoryStateUpdated.addListener((details) => {
        if (details.frameId === 0) {
            handleUrlChange(details.tabId, details.url);
        }
    });

    chrome.webNavigation.onReferenceFragmentUpdated.addListener((details) => {
        if (details.frameId === 0) {
            handleUrlChange(details.tabId, details.url);
        }
    });

    chrome.tabs.onRemoved.addListener((tabId) => {
        tabUrls.delete(tabId);
    });

    void initializeOpenTabs().catch((error: unknown) => {
        console.error("[Discheese] 초기 탭 상태 확인 실패", error);
    });
}
