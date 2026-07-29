import type {
    PresencePayload,
    SocketRequest,
    SocketResponse,
    SocketStatus,
    SocketStatusChanged,
} from "../shared/socket";
import Socket from "./socket";

const KEEP_ALIVE_INTERVAL = 20_000;
const RESPONSE_TIMEOUT = 10_000;

const socket = new Socket();
let keepAliveTimer: ReturnType<typeof setInterval> | null = null;

function getStatus(): SocketStatus {
    return {
        connected: socket.connected,
        port: socket.port,
    };
}

function notifyStatus(): void {
    const message: SocketStatusChanged = {
        type: "socket:statusChanged",
        status: getStatus(),
    };

    void chrome.runtime.sendMessage(message).catch(() => {});
}

function success(): SocketResponse {
    return {
        ok: true,
        ...getStatus(),
    };
}

function failure(error: unknown): SocketResponse {
    return {
        ok: false,
        error: error instanceof Error ? error.message : String(error),
        ...getStatus(),
    };
}

function stopKeepAlive(): void {
    if (keepAliveTimer === null) {
        return;
    }

    clearInterval(keepAliveTimer);
    keepAliveTimer = null;
}

function startKeepAlive(): void {
    stopKeepAlive();

    keepAliveTimer = setInterval(() => {
        try {
            socket.send("ping");
        } catch {
            stopKeepAlive();
            notifyStatus();
        }
    }, KEEP_ALIVE_INTERVAL);
}

async function connect(): Promise<void> {
    await socket.connect();
    startKeepAlive();
    notifyStatus();
}

async function reconnect(port?: number): Promise<void> {
    stopKeepAlive();
    socket.disconnect();

    if (port !== undefined) {
        if (!Number.isInteger(port) || port < 1 || port > 65535) {
            throw new Error("포트는 1부터 65535 사이의 정수여야 합니다.");
        }

        socket.port = port;
    }

    await connect();
}

function waitForResponse(
    event: "pong" | "done",
    send: () => void,
): Promise<void> {
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            cleanup();
            reject(new Error("Discheese 서버의 응답 시간이 초과되었습니다."));
        }, RESPONSE_TIMEOUT);

        const offSuccess = socket.on(event, () => {
            cleanup();
            resolve();
        });

        const offError = socket.on("error", (message) => {
            cleanup();
            reject(new Error(message));
        });

        const cleanup = () => {
            clearTimeout(timeout);
            offSuccess();
            offError();
        };

        try {
            send();
        } catch (error) {
            cleanup();
            reject(error);
        }
    });
}

function setPresence(payload: PresencePayload): Promise<void> {
    return waitForResponse("done", () => {
        socket.send("presence", payload);
    });
}

function clearPresence(): Promise<void> {
    return waitForResponse("done", () => {
        socket.send("clear");
    });
}

function ping(): Promise<void> {
    return waitForResponse("pong", () => {
        socket.send("ping");
    });
}

function isPresencePayload(value: unknown): value is PresencePayload {
    if (!value || typeof value !== "object") {
        return false;
    }

    const payload = value as Partial<PresencePayload>;

    return (
        typeof payload.streamer === "string" &&
        typeof payload.title === "string" &&
        typeof payload.url === "string" &&
        typeof payload.profileImageUrl === "string"
    );
}

function isSocketRequest(value: unknown): value is SocketRequest {
    if (!value || typeof value !== "object" || !("type" in value)) {
        return false;
    }

    const request = value as {
        type?: unknown;
        payload?: unknown;
        port?: unknown;
    };

    switch (request.type) {
        case "socket:status":
        case "socket:disconnect":
        case "socket:ping":
        case "socket:clearPresence":
            return true;
        case "socket:reconnect":
            return request.port === undefined || typeof request.port === "number";
        case "socket:setPresence":
            return isPresencePayload(request.payload);
        default:
            return false;
    }
}

async function handleRequest(request: SocketRequest): Promise<SocketResponse> {
    try {
        switch (request.type) {
            case "socket:status":
                break;
            case "socket:reconnect":
                await reconnect(request.port);
                break;
            case "socket:disconnect":
                stopKeepAlive();
                socket.disconnect();
                notifyStatus();
                break;
            case "socket:ping":
                await ping();
                break;
            case "socket:setPresence":
                await setPresence(request.payload);
                break;
            case "socket:clearPresence":
                await clearPresence();
                break;
        }

        return success();
    } catch (error) {
        return failure(error);
    }
}

export function registerSocket(): void {
    socket.on("error", (message) => {
        console.error("[Discheese] WebSocket 오류:", message);
        notifyStatus();
    });

    chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
        if (!isSocketRequest(message)) {
            return false;
        }

        void handleRequest(message).then(sendResponse);
        return true;
    });

    void connect().catch((error: unknown) => {
        console.error("[Discheese] WebSocket 연결 실패:", error);
        notifyStatus();
    });
}
