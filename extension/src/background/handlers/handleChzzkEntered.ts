import type { ChzzkEvent } from "../types";
import { isChzzkLiveUrl } from "../url";
import { handleChzzkNavigated } from "./handleChzzkNavigated";

export async function handleChzzkEntered(event: ChzzkEvent): Promise<void> {
    console.log("handleChzzkEntered", event);
    if (!event.socket.connected) await event.socket.waitUntilConnected();

    if (isChzzkLiveUrl(event.url)) {
        return await handleChzzkNavigated(event);
    }

    event.socket.send("presence", {
        "details": "볼 라이브 찾는 중",
        "streamer": "치지직",
        "url": "https://chzzk.naver.com",
        "profileImageUrl": "chzzk",
        "smallImage": false,
        "statusDisplay": "state"
    });

    return;
}
