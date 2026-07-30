const CHZZK_HOST = "chzzk.naver.com";
export const CHZZK_LIVE_URL_PREFIX = "https://chzzk.naver.com/live/";

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
    return url?.startsWith(CHZZK_LIVE_URL_PREFIX) ?? false;
}
