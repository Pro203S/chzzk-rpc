const CHZZK_HOST = "chzzk.naver.com";

export function isChzzkUrl(url?: string): boolean {
    if (!url) {
        return false;
    }

    try {
        const parsedUrl = new URL(url);

        return (
            parsedUrl.protocol === "https:" && parsedUrl.hostname === CHZZK_HOST
        );
    } catch {
        return false;
    }
}

export function isChzzkLiveUrl(url?: string): boolean {
    return url?.startsWith("https://chzzk.naver.com/live/") ?? false;
}

export function isChzzkClipUrl(url?: string): boolean {
    return url?.startsWith("https://chzzk.naver.com/clips/") ?? false;
}
