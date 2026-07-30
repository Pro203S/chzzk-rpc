export const SERVER_VERSION = "v1.0.0";

export type StatusDisplay = "name" | "state" | "details";

export interface PresencePayload {
    streamer: string;
    details: string;
    url: string;
    profileImageUrl: string;
    smallImage?: boolean;
    statusDisplay?: StatusDisplay;
}

export interface DiscordUser {
    username: string;
    displayName: string;
    avatarUrl: string;
}

export interface SocketStatus {
    connected: boolean;
    port: number;
    socketError: string | null;
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
        type: "socket:user";
    }
    | {
        type: "socket:version";
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
        user?: DiscordUser | null;
        version?: string;
    } & SocketStatus)
    | ({
        ok: false;
        error: string;
    } & SocketStatus);
