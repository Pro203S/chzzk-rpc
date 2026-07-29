export interface PresencePayload {
    streamer: string;
    title: string;
    url: string;
    profileImageUrl: string;
}

export interface SocketStatus {
    connected: boolean;
    port: number;
}

export interface SocketStatusChanged {
    type: "socket:statusChanged";
    status: SocketStatus;
}

export type SocketRequest =
    | {
        type: "socket:status";
    }
    | {
        type: "socket:connect";
    }
    | {
        type: "socket:reconnect";
        port?: number;
    }
    | {
        type: "socket:disconnect";
    }
    | {
        type: "socket:ping";
    }
    | {
        type: "socket:setPresence";
        payload: PresencePayload;
    }
    | {
        type: "socket:clearPresence";
    };

export type SocketResponse =
    | ({
        ok: true;
    } & SocketStatus)
    | ({
        ok: false;
        error: string;
    } & SocketStatus);
