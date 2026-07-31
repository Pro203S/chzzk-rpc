export const DEFAULT_PORT = 58127;

const PORT_STORAGE_KEY = "port";

export function isValidPort(port: unknown): port is number {
    return Number.isInteger(port) && (port as number) >= 1 && (port as number) <= 65535;
}

export async function loadPort(): Promise<number> {
    const result = await chrome.storage.local.get(PORT_STORAGE_KEY);
    const port = result[PORT_STORAGE_KEY];

    if (isValidPort(port)) {
        return port;
    }

    await savePort(DEFAULT_PORT);
    return DEFAULT_PORT;
}

export async function savePort(port: number): Promise<void> {
    if (!isValidPort(port)) {
        throw new Error("포트는 1부터 65535 사이의 정수여야 합니다.");
    }

    await chrome.storage.local.set({
        [PORT_STORAGE_KEY]: port,
    });
}
