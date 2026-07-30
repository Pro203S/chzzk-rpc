import type { ChzzkEvent } from "../types";

export function handleChzzkNavigated(event: ChzzkEvent): void {
    console.log("handleChzzkNavigated", event);
    if (!event.socket.connected) return;


}
