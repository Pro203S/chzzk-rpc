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
