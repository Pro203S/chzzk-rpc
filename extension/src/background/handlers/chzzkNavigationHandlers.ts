export interface ChzzkNavigationEvent {
    tabId: number;
    url: string;
    previousUrl?: string;
}

function safelyUpdateAction(update: Promise<void>): void {
    void update.catch((error: unknown) => {
        console.error("[Discheese] action 상태 변경 실패", error);
    });
}

export function handleChzzkEntered(event: ChzzkNavigationEvent): void {
    safelyUpdateAction(chrome.action.enable(event.tabId));
    console.log("[Discheese] 치지직 진입", event);
}

export function handleChzzkLeft(event: ChzzkNavigationEvent): void {
    safelyUpdateAction(chrome.action.disable(event.tabId));
    console.log("[Discheese] 치지직 이탈", event);
}

export function handleChzzkNavigated(event: ChzzkNavigationEvent): void {
    console.log("[Discheese] 치지직 내부 이동", event);
}

export function handleOutsideChzzk(event: ChzzkNavigationEvent): void {
    safelyUpdateAction(chrome.action.disable(event.tabId));
    console.log("[Discheese] 치지직 외부", event);
}
