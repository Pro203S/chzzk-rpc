import type { ChzzkEvent } from "../types";

export async function handleChzzkLeft(event: ChzzkEvent): Promise<void> {
    console.log("handleChzzkLeft", event);
    if (!event.socket.connected) await event.socket.waitUntilConnected();

    event.socket.send("clear");
}
