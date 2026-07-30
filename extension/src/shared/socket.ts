export interface PresencePayload {
    streamer: string;
    title: string;
    url: string;
    profileImageUrl: string;
}

export interface DiscordUser {
    username: string;
    displayName: string;
    avatarUrl: string;
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
