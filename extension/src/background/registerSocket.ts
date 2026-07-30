import type {
    DiscordUser,
    PresencePayload,
    SocketRequest,
    SocketResponse,
    SocketStatus,
    SocketStatusChanged,
} from "../shared/socket";
import Socket from "./socket";

const KEEP_ALIVE_INTERVAL = 20_000;
const RECONNECT_INTERVAL = 1_000;
const RESPONSE_TIMEOUT = 10_000;

const socket = new Socket();
let keepAliveTimer: ReturnType<typeof setInterval> | null = null;
let reconnectTimer: ReturnType<typeof setTimeout> | null = null;
let reconnectEnabled = false;

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

function success(user?: DiscordUser | null): SocketResponse {
    return {
        ok: true,
        ...(user !== undefined ? { user } : {}),
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

function stopReconnect(): void {
    if (reconnectTimer === null) {
        return;
    }

    clearTimeout(reconnectTimer);
    reconnectTimer = null;
}

function scheduleReconnect(): void {
    if (
        !reconnectEnabled ||
        socket.connected ||
        reconnectTimer !== null
    ) {
        return;
    }

    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;

        if (!reconnectEnabled || socket.connected) {
            return;
        }

        void connect().catch(() => {});
    }, RECONNECT_INTERVAL);
}

function startKeepAlive(): void {
    stopKeepAlive();

    keepAliveTimer = setInterval(() => {
        try {
            socket.send("ping");
        } catch {
            stopKeepAlive();
            notifyStatus();
            scheduleReconnect();
        }
    }, KEEP_ALIVE_INTERVAL);
}

async function connect(): Promise<void> {
    reconnectEnabled = true;
    stopReconnect();

    try {
        await socket.connect();
        startKeepAlive();
        notifyStatus();
    } catch (error) {
        notifyStatus();
        scheduleReconnect();
        throw error;
    }
}

async function reconnect(port?: number): Promise<void> {
    reconnectEnabled = true;
    stopReconnect();
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

function disconnect(): void {
    reconnectEnabled = false;
    stopReconnect();
    stopKeepAlive();
    socket.disconnect();
    notifyStatus();
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

function getUser(): Promise<DiscordUser | null> {
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            cleanup();
            reject(new Error("Discheese 서버의 응답 시간이 초과되었습니다."));
        }, RESPONSE_TIMEOUT);

        const offUser = socket.on("user", (user) => {
            cleanup();
            resolve(user);
        });

        const offError = socket.on("error", (message) => {
            cleanup();
            reject(new Error(message));
        });

        const cleanup = () => {
            clearTimeout(timeout);
            offUser();
            offError();
        };

        try {
            socket.send("user");
        } catch (error) {
            cleanup();
            reject(error);
        }
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
        case "socket:connect":
        case "socket:disconnect":
        case "socket:ping":
        case "socket:user":
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
            case "socket:connect":
                await connect();
                break;
            case "socket:reconnect":
                await reconnect(request.port);
                break;
            case "socket:disconnect":
                disconnect();
                break;
            case "socket:ping":
                await ping();
                break;
            case "socket:user":
                return success(await getUser());
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
    socket.on("error", () => {
        notifyStatus();

        if (!socket.connected) {
            stopKeepAlive();
            scheduleReconnect();
        }
    });

    chrome.runtime.onMessage.addListener((message, _sender, sendResponse) => {
        if (!isSocketRequest(message)) {
            return false;
        }

        void handleRequest(message).then(sendResponse).catch(() => {});
        return true;
    });

    void connect().catch(() => {});
}
