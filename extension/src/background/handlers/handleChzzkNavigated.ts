import type { ChzzkEvent } from "../types";

export function handleChzzkNavigated(event: ChzzkEvent): void {
    console.log("[Discheese] 치지직 내부 이동", event);
}
