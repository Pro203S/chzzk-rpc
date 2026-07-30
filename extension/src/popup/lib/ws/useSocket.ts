import { useCallback, useEffect, useState } from "react";
import type {
    DiscordUser,
    PresencePayload,
    SocketStatus,
    SocketStatusChanged,
} from "../../../shared/socket";
import { backgroundSocket } from "./socket";

export default function useSocket() {
    const [status, setStatus] = useState<SocketStatus>({
        connected: false,
        port: 58127,
        socketError: null,
    });
    const [error, setError] = useState<string | null>(null);

    const run = useCallback(
        async (action: () => Promise<SocketStatus>): Promise<void> => {
            try {
                const nextStatus = await action();
                setStatus(nextStatus);
                setError(nextStatus.socketError);
            } catch (reason) {
                setError(reason instanceof Error ? reason.message : String(reason));
            }
        },
        [],
    );

    const refresh = useCallback(() => {
        return run(() => backgroundSocket.status());
    }, [run]);

    const connect = useCallback(() => {
        return run(() => backgroundSocket.connect());
    }, [run]);

    const reconnect = useCallback((port?: number) => {
        return run(() => backgroundSocket.reconnect(port));
    }, [run]);

    const disconnect = useCallback(() => {
        return run(() => backgroundSocket.disconnect());
    }, [run]);

    const ping = useCallback(() => {
        return run(() => backgroundSocket.ping());
    }, [run]);

    const user = useCallback(async (): Promise<DiscordUser | null> => {
        try {
            const nextUser = await backgroundSocket.user();
            return nextUser;
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : String(reason));
            return null;
        }
    }, []);

    const getVersion = useCallback(async (): Promise<string> => {
        try {
            const version = await backgroundSocket.getVersion();
            return version;
        } catch (reason) {
            setError(reason instanceof Error ? reason.message : String(reason));
            throw reason;
        }
    }, []);

    const setPresence = useCallback((payload: PresencePayload) => {
        return run(() => backgroundSocket.setPresence(payload));
    }, [run]);

    const clearPresence = useCallback(() => {
        return run(() => backgroundSocket.clearPresence());
    }, [run]);

    useEffect(() => {
        const handleMessage = (message: unknown) => {
            if (
                !message ||
                typeof message !== "object" ||
                !("type" in message) ||
                message.type !== "socket:statusChanged" ||
                !("status" in message)
            ) {
                return;
            }

            const statusChanged = message as SocketStatusChanged;
            setStatus(statusChanged.status);
            setError(statusChanged.status.socketError);
        };

        chrome.runtime.onMessage.addListener(handleMessage);
        void refresh().catch(() => {});

        return () => {
            chrome.runtime.onMessage.removeListener(handleMessage);
        };
    }, [refresh]);

    return {
        ...status,
        error,
        refresh,
        connect,
        reconnect,
        disconnect,
        ping,
        user,
        getVersion,
        setPresence,
        clearPresence,
    };
}
