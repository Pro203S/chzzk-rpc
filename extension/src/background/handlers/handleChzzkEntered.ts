import type { ChzzkEvent } from "../types";

export function handleChzzkEntered(event: ChzzkEvent): void {
    console.log("handleChzzkEntered", event);
    if (!event.socket.connected) return;

    
}
