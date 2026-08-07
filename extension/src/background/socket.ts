import {
    SERVER_VERSION,
    type DiscordUser,
    type PresencePayload,
} from "../shared/socket";
import { DEFAULT_PORT } from "../shared/port";

export type ReceivedEvent =
    | "pong"
    | "done"
    | "error"
    | "errorCleared"
    | "updating"
    | "user"
    | "version";
export type SentEvent =
    | "ping"
    | "presence"
    | "clear"
    | "user"
    | "version"
    | "update";

interface ReceivedEventPayloads {
    pong: undefined;
    done: undefined;
    error: string;
    errorCleared: undefined;
    updating: undefined;
    user: DiscordUser | null;
    version: string;
}

interface SentEventPayloads {
    ping: undefined;
    presence: PresencePayload;
    clear: undefined;
    user: undefined;
    version: undefined;
    update: string;
}

type Listener<Event extends ReceivedEvent> =
    ReceivedEventPayloads[Event] extends undefined
    ? () => void
    : (payload: ReceivedEventPayloads[Event]) => void;

type SendArguments<Event extends SentEvent> =
    SentEventPayloads[Event] extends undefined
    ? []
    : [payload: SentEventPayloads[Event]];

type StoredListener = (...args: never[]) => void;

const SERVER_PROBE_TIMEOUT = 1_000;
const RESPONSE_TIMEOUT = 10_000;
const UPDATE_RESPONSE_TIMEOUT = 120_000;
const VERSION_PATTERN = /^v\d+\.\d+\.\d+$/;

export default class Socket {
    private readonly listeners = new Map<ReceivedEvent, Set<StoredListener>>();
    private readonly connectionWaiters = new Set<() => void>();
    private webSocket: WebSocket | null = null;
    private connection: Promise<void> | null = null;
    private probeController: AbortController | null = null;
    private socketError: string | null = null;
    private versionError: Error | null = null;
    private versionVerified = false;

    public constructor(public port: number = DEFAULT_PORT) { }

    public get connected(): boolean {
        return (
            this.webSocket?.readyState === WebSocket.OPEN &&
            this.versionVerified
        );
    }

    public get error(): string | null {
        return this.socketError;
    }

    public connect(): Promise<void> {
        if (this.connected) {
            return Promise.resolve();
        }

        if (this.connection) {
            return this.connection;
        }

        const connection = this.open().catch((error: unknown) => {
            if (this.connection === connection) {
                this.emit(
                    "error",
                    error instanceof Error ? error.message : String(error),
                );
            }

            throw error;
        });

        this.connection = connection;

        void connection
            .finally(() => {
                if (this.connection === connection) {
                    this.connection = null;
                }
            })
            .catch(() => { });

        return connection;
    }

    public disconnect(clearError: boolean = true): void {
        const webSocket = this.webSocket;

        this.connection = null;
        this.probeController?.abort();
        this.probeController = null;

        if (clearError) {
            this.socketError = null;
        }

        this.versionError = null;
        this.versionVerified = false;

        if (!webSocket) {
            return;
        }

        this.webSocket = null;
        webSocket.removeEventListener("message", this.handleMessage);
        webSocket.removeEventListener("error", this.handleError);
        webSocket.removeEventListener("close", this.handleClose);

        if (
            webSocket.readyState === WebSocket.CONNECTING ||
            webSocket.readyState === WebSocket.OPEN
        ) {
            webSocket.close(1000, "Disconnected by client");
        }
    }

    private async open(): Promise<void> {
        const probeController = new AbortController();
        const probeTimeout = setTimeout(
            () => probeController.abort(),
            SERVER_PROBE_TIMEOUT,
        );

        this.probeController = probeController;

        try {
            await fetch(`http://localhost:${this.port}/`, {
                method: "HEAD",
                mode: "no-cors",
                cache: "no-store",
                signal: probeController.signal,
            });
        } catch {
            throw new Error(
                `포트 ${this.port}의 Discheese 서버에 연결하지 못했습니다.`,
            );
        } finally {
            clearTimeout(probeTimeout);

            if (this.probeController === probeController) {
                this.probeController = null;
            }
        }

        if (probeController.signal.aborted || this.connection === null) {
            throw new Error("Discheese 서버 연결이 취소되었습니다.");
        }

        const webSocket = new WebSocket(`ws://localhost:${this.port}/`);
        this.webSocket = webSocket;

        webSocket.addEventListener("message", this.handleMessage);
        webSocket.addEventListener("error", this.handleError);
        webSocket.addEventListener("close", this.handleClose);

        await new Promise<void>((resolve, reject) => {
            const handleOpen = () => {
                removeConnectionListeners();
                resolve();
            };

            const handleConnectionError = () => {
                removeConnectionListeners();
                reject(
                    new Error(
                        `포트 ${this.port}의 Discheese 서버에 연결하지 못했습니다.`,
                    ),
                );
            };

            const handleConnectionClose = () => {
                removeConnectionListeners();
                reject(
                    new Error(
                        "Discheese 서버에 연결되기 전에 연결이 종료되었습니다.",
                    ),
                );
            };

            const removeConnectionListeners = () => {
                webSocket.removeEventListener("open", handleOpen);
                webSocket.removeEventListener("error", handleConnectionError);
                webSocket.removeEventListener("close", handleConnectionClose);
            };

            webSocket.addEventListener("open", handleOpen);
            webSocket.addEventListener("error", handleConnectionError);
            webSocket.addEventListener("close", handleConnectionClose);
        });

        try {
            await this.getVersion();
        } catch (error) {
            if (this.webSocket === webSocket) {
                this.disconnect();
            }

            throw error;
        }
    }

    public waitUntilConnected(): Promise<void> {
        if (this.connected) {
            return Promise.resolve();
        }

        return new Promise((resolve) => {
            this.connectionWaiters.add(resolve);

            if (this.connected) {
                this.connectionWaiters.delete(resolve);
                resolve();
            }
        });
    }

    public on<Event extends ReceivedEvent>(
        event: Event,
        listener: Listener<Event>,
    ): () => void {
        let listeners = this.listeners.get(event);

        if (!listeners) {
            listeners = new Set();
            this.listeners.set(event, listeners);
        }

        listeners.add(listener as StoredListener);

        return () => {
            listeners.delete(listener as StoredListener);

            if (listeners.size === 0) {
                this.listeners.delete(event);
            }
        };
    }

    public send<Event extends SentEvent>(
        event: Event,
        ...args: SendArguments<Event>
    ): void {
        if (!this.webSocket || this.webSocket.readyState !== WebSocket.OPEN) {
            throw new Error("Discheese 서버에 연결되어 있지 않습니다.");
        }

        if (event === "presence") {
            const payload = args[0] as PresencePayload;
            const fields = [
                payload.streamer,
                payload.details,
                payload.url,
                payload.profileImageUrl,
            ];

            if (fields.some((field) => field.includes("\u0007"))) {
                throw new Error("Presence payload에는 \\u0007 문자를 사용할 수 없습니다.");
            }

            if (
                payload.smallImage !== undefined ||
                payload.statusDisplay !== undefined
            ) {
                fields.push(String(payload.smallImage ?? false));
            }

            if (payload.statusDisplay !== undefined) {
                fields.push(payload.statusDisplay);
            }

            this.webSocket.send(`presence ${fields.join("\u0007")}`);
            return;
        }

        if (event === "update") {
            this.webSocket.send(`update ${args[0] as string}`);
            return;
        }

        this.webSocket.send(event);
    }

    public getVersion(): Promise<string> {
        if (
            this.versionError &&
            this.webSocket?.readyState !== WebSocket.OPEN
        ) {
            return Promise.reject(this.versionError);
        }

        return new Promise((resolve, reject) => {
            let updateRequested = false;
            let timeout = setTimeout(() => {
                cleanup();
                reject(new Error("Discheese 서버의 응답 시간이 초과되었습니다."));
            }, RESPONSE_TIMEOUT);

            const offVersion = this.on("version", (version) => {
                if (version !== SERVER_VERSION) {
                    if (updateRequested) {
                        return;
                    }

                    updateRequested = true;
                    clearTimeout(timeout);

                    timeout = setTimeout(() => {
                        cleanup();

                        const message =
                            "서버 자동 업데이트 응답 시간이 초과되었습니다.";

                        const error = new Error(message);
                        this.versionError = error;
                        this.emit("error", message);
                        reject(error);
                    }, UPDATE_RESPONSE_TIMEOUT);

                    try {
                        this.send("update", SERVER_VERSION);
                    } catch (error) {
                        cleanup();
                        reject(error);
                    }

                    return;
                }

                cleanup();

                this.versionError = null;
                this.versionVerified = true;
                this.emit("errorCleared");
                this.resolveConnectionWaiters();
                resolve(version);
            });

            const offUpdating = this.on("updating", () => {
                cleanup();

                const message = "서버를 자동 업데이트하고 있습니다.";
                const error = new Error(message);

                this.versionError = error;
                this.emit("error", message);
                reject(error);
            });

            const offError = this.on("error", (message) => {
                cleanup();

                if (
                    updateRequested &&
                    message === "Discheese 서버와 연결이 종료되었습니다."
                ) {
                    message =
                        "이 서버는 자동 업데이트를 지원하지 않습니다. " +
                        "서버를 한 번 수동으로 업데이트해주세요.";
                }

                reject(new Error(message));
            });

            const cleanup = () => {
                clearTimeout(timeout);
                offVersion();
                offUpdating();
                offError();
            };

            try {
                this.send("version");
            } catch (error) {
                cleanup();
                reject(error);
            }
        });
    }

    private readonly handleMessage = (messageEvent: MessageEvent<unknown>) => {
        if (typeof messageEvent.data !== "string") {
            this.emit("error", "서버에서 텍스트가 아닌 메시지를 받았습니다.");
            return;
        }

        if (messageEvent.data === "pong") {
            this.emit("pong");
            return;
        }

        if (messageEvent.data === "done") {
            this.emit("done");
            return;
        }

        if (messageEvent.data === "updating") {
            this.emit("updating");
            return;
        }

        if (
            messageEvent.data === "error" ||
            messageEvent.data.startsWith("error ")
        ) {
            this.emit("error", messageEvent.data.slice("error".length).trimStart());
            return;
        }

        if (messageEvent.data === "error-clear") {
            this.emit("errorCleared");
            return;
        }

        if (messageEvent.data === "null") {
            this.emit("user", null);
            return;
        }

        if (VERSION_PATTERN.test(messageEvent.data)) {
            this.emit("version", messageEvent.data);
            return;
        }

        const user = messageEvent.data.split("\u0007");

        if (user.length === 3) {
            this.emit("user", {
                username: user[0],
                displayName: user[1],
                avatarUrl: user[2],
            });
            return;
        }

        this.emit("error", `알 수 없는 서버 응답입니다: ${messageEvent.data}`);
    };

    private readonly handleError = () => {
        this.emit("error", "Discheese 서버와 통신하는 중 오류가 발생했습니다.");
    };

    private readonly handleClose = (event: CloseEvent) => {
        if (event.currentTarget === this.webSocket) {
            this.webSocket = null;
            this.versionVerified = false;
            this.emit("error", "Discheese 서버와 연결이 종료되었습니다.");
        }
    };

    private resolveConnectionWaiters(): void {
        for (const resolve of this.connectionWaiters) {
            resolve();
        }

        this.connectionWaiters.clear();
    }

    private emit(
        event: "pong" | "done" | "errorCleared" | "updating",
    ): void;
    private emit(event: "error", payload: string): void;
    private emit(event: "user", payload: DiscordUser | null): void;
    private emit(event: "version", payload: string): void;
    private emit(
        event: ReceivedEvent,
        payload?: string | DiscordUser | null,
    ): void {
        if (event === "error") {
            this.socketError =
                typeof payload === "string" ? payload : "";
        } else if (event === "errorCleared") {
            this.socketError = null;
        }

        const listeners = this.listeners.get(event);

        if (!listeners) {
            return;
        }

        for (const listener of listeners) {
            if (event === "error" || event === "version") {
                (listener as (message: string) => void)(
                    typeof payload === "string" ? payload : "",
                );
                continue;
            }

            if (event === "user") {
                (listener as (user: DiscordUser | null) => void)(
                    typeof payload === "object" ? payload : null,
                );
                continue;
            }

            (listener as () => void)();
        }
    }
}
