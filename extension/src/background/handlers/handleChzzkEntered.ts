import type { ChzzkEvent } from "../types";

export async function handleChzzkEntered(event: ChzzkEvent) {
    console.log("handleChzzkEntered", event);
    if (!event.socket.connected) await event.socket.waitUntilConnected();

    event.socket.on("error", console.error);

    event.socket.send("presence", {
        "title": "라이브 찾는 중",
        "streamer": "치지직",
        "url": "https://chzzk.naver.com",
        "profileImageUrl": "chzzk"
    });
}
