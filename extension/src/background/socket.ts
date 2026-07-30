import type { DiscordUser, PresencePayload } from "../shared/socket";

export type ReceivedEvent = "pong" | "done" | "error" | "user";
export type SentEvent = "ping" | "presence" | "clear" | "user";

interface ReceivedEventPayloads {
    pong: undefined;
    done: undefined;
    error: string;
    user: DiscordUser | null;
}

interface SentEventPayloads {
    ping: undefined;
    presence: PresencePayload;
    clear: undefined;
    user: undefined;
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

export default class Socket {
    private readonly listeners = new Map<ReceivedEvent, Set<StoredListener>>();
    private webSocket: WebSocket | null = null;
    private connection: Promise<void> | null = null;
    private probeController: AbortController | null = null;

    public constructor(public port: number = 58127) {}

    public get connected(): boolean {
        return this.webSocket?.readyState === WebSocket.OPEN;
    }

    public connect(): Promise<void> {
        if (this.connected) {
            return Promise.resolve();
        }

        if (this.connection) {
            return this.connection;
        }

        const connection = this.open();

        this.connection = connection;

        void connection
            .finally(() => {
                if (this.connection === connection) {
                    this.connection = null;
                }
            })
            .catch(() => {});

        return connection;
    }

    public disconnect(): void {
        const webSocket = this.webSocket;

        this.connection = null;
        this.probeController?.abort();
        this.probeController = null;

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
                payload.title,
                payload.url,
                payload.profileImageUrl,
            ];

            if (fields.some((field) => field.includes("\u0007"))) {
                throw new Error("Presence payload에는 \\u0007 문자를 사용할 수 없습니다.");
            }

            this.webSocket.send(`presence ${fields.join("\u0007")}`);
            return;
        }

        this.webSocket.send(event);
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

        if (
            messageEvent.data === "error" ||
            messageEvent.data.startsWith("error ")
        ) {
            this.emit("error", messageEvent.data.slice("error".length).trimStart());
            return;
        }

        if (messageEvent.data === "null") {
            this.emit("user", null);
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
            this.emit("error", "Discheese 서버와 연결이 종료되었습니다.");
        }
    };

    private emit(event: "pong" | "done"): void;
    private emit(event: "error", payload: string): void;
    private emit(event: "user", payload: DiscordUser | null): void;
    private emit(
        event: ReceivedEvent,
        payload?: string | DiscordUser | null,
    ): void {
        const listeners = this.listeners.get(event);

        if (!listeners) {
            return;
        }

        for (const listener of listeners) {
            if (event === "error") {
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
