import type {
    DiscordUser,
    PresencePayload,
    SocketRequest,
    SocketResponse,
    SocketStatus,
} from "../../../shared/socket";

type SuccessfulSocketResponse = Extract<SocketResponse, { ok: true }>;

async function request(
    request: SocketRequest,
): Promise<SuccessfulSocketResponse> {
    const response = await chrome.runtime.sendMessage(request) as
        SocketResponse | undefined;

    if (!response) {
        throw new Error("Background에서 응답을 받지 못했습니다.");
    }

    if (!response.ok) {
        throw new Error(response.error);
    }

    return response;
}

async function send(requestData: SocketRequest): Promise<SocketStatus> {
    const response = await request(requestData);

    return {
        connected: response.connected,
        port: response.port,
    };
}

export const backgroundSocket = {
    status(): Promise<SocketStatus> {
        return send({
            type: "socket:status",
        });
    },

    connect(): Promise<SocketStatus> {
        return send({
            type: "socket:connect",
        });
    },

    reconnect(port?: number): Promise<SocketStatus> {
        return send({
            type: "socket:reconnect",
            port,
        });
    },

    disconnect(): Promise<SocketStatus> {
        return send({
            type: "socket:disconnect",
        });
    },

    ping(): Promise<SocketStatus> {
        return send({
            type: "socket:ping",
        });
    },

    async user(): Promise<DiscordUser | null> {
        const response = await request({
            type: "socket:user",
        });

        return response.user ?? null;
    },

    async getVersion(): Promise<string> {
        const response = await request({
            type: "socket:version",
        });

        if (!response.version) {
            throw new Error("Background에서 서버 버전을 받지 못했습니다.");
        }

        return response.version;
    },

    setPresence(payload: PresencePayload): Promise<SocketStatus> {
        return send({
            type: "socket:setPresence",
            payload,
        });
    },

    clearPresence(): Promise<SocketStatus> {
        return send({
            type: "socket:clearPresence",
        });
    },
};
