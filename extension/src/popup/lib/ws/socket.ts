import type {
    PresencePayload,
    SocketRequest,
    SocketResponse,
    SocketStatus,
} from "../../../shared/socket";

async function send(request: SocketRequest): Promise<SocketStatus> {
    const response = await chrome.runtime.sendMessage(request) as
        SocketResponse | undefined;

    if (!response) {
        throw new Error("Background에서 응답을 받지 못했습니다.");
    }

    if (!response.ok) {
        throw new Error(response.error);
    }

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
