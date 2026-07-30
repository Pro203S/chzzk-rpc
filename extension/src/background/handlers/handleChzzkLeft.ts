import type { ChzzkEvent } from "../types";

export function handleChzzkLeft(event: ChzzkEvent): void {
    console.log("handleChzzkLeft", event);
    if (!event.socket.connected) return;


}
