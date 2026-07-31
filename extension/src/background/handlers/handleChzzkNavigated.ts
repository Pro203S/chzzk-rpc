import type { ChzzkEvent } from "../types";
import { isChzzkClipUrl, isChzzkLiveUrl } from "../url";
import { handleChzzkEntered } from "./handleChzzkEntered";

type LiveResponse = {
    abroadCountry: boolean;
    accumulateCount: number;
    adult: boolean;
    allowSubscriberInFollowerMode: boolean;
    blindType: null;
    categoryType: string;
    channelId: string;
    chatActive: boolean;
    chatAvailableCondition: string;
    chatAvailableGroup: string;
    chatChannelId: string;
    chatDonationRankingExposure: boolean;
    chatEmojiMode: boolean;
    chatSlowModeSec: number;
    clipActive: boolean;
    closeDate: string | null;
    concurrentUserCount: number;
    cvExposure: boolean;
    donationBoardNo: number | null;
    donationCampaignId: number | null;
    dropsCampaignNo: number | null;
    faultStatus: string | null;
    krOnlyViewing: boolean;
    lastAdultReleaseDate: string | null;
    lastKrOnlyViewingReleaseDate: string | null;
    lastTvAppAllowedDate: string | null;
    liveCategory: string;
    liveCategoryValue: string;
    liveConnecting: boolean;
    livePollingStatusJson: string;
    liveTitle: string;
    liveTokenList: string[];
    logPowerActive: boolean;
    logPowerRankingExposure: boolean;
    membershipBenefitType: string;
    minFollowerMinute: number;
    openDate: string;
    paidPromotion: boolean;
    playerRecommendContent: {
        categoryLives: unknown[];
        channelLatestVideos: unknown[];
    };
    skipPreRollAd: boolean;
    sportsMatch: unknown | null;
    status: string;
    streamerShopCatalogTagActive: boolean;
    tags: string[];
    timeMachineActive: boolean;
    tvAppViewingPolicyType: string;
    userAdultStatus: string;
    watchPartyNo: number | null;
    watchPartyTag: string | null;
    watchPartyType: string | null;
};

type ChannelResponse = {
    "channelId": string,
    "channelName": string,
    "channelImageUrl": string,
    "verifiedMark": false,
    "channelType": string,
    "channelDescription": string,
    "followerCount": 6040,
    "openLive": boolean,
    "subscriptionAvailability": boolean,
    "subscriptionPaymentAvailability": {
        "iapAvailability": boolean,
        "iabAvailability": boolean
    },
    "adMonetizationAvailability": boolean,
    "activatedChannelBadgeIds": [],
    "paidProductSaleAllowed": boolean
};

type ClipResponse = {
    clipUID: string;
    videoId: string;
    clipTitle: string;
    thumbnailImageUrl: string;
    categoryType: string;
    clipCategory: string;
    duration: number;
    adult: boolean;
    blindType: string | null;
    krOnlyViewing: boolean;
    vodStatus: string;
    recId: string;
    createdDate: string;
    optionalProperty: {
        makerChannel: {
            channelId: string;
            channelName: string;
            channelImageUrl: string;
            verifiedMark: boolean;
        };
        ownerChannel: {
            channelId: string;
            channelName: string;
            channelImageUrl: string;
            verifiedMark: boolean;
        };
    };
    commentActive: boolean;
    userAdultStatus: string | null;
};

type ChzzkApiResponse<T> = {
    content: T;
};

async function fetchChzzkApi<T>(path: string): Promise<T> {
    const response = await fetch(`https://api.chzzk.naver.com${path}`);

    if (!response.ok) {
        throw new Error(`치지직 API 요청에 실패했습니다. (${response.status})`);
    }

    const result = await response.json() as ChzzkApiResponse<T>;
    return result.content;
}

function getLiveInfo(liveId: string): Promise<LiveResponse> {
    return fetchChzzkApi(
        `/polling/v3.1/channels/${encodeURIComponent(liveId)}/live-status`,
    );
}

function getChannelInfo(liveId: string): Promise<ChannelResponse> {
    return fetchChzzkApi(
        `/service/v1/channels/${encodeURIComponent(liveId)}`,
    );
}

function getClipInfo(clipId: string): Promise<ClipResponse> {
    return fetchChzzkApi(
        `/service/v1/clips/${encodeURIComponent(clipId)}/detail` +
        "?optionalProperties=MAKER_CHANNEL&optionalProperties=OWNER_CHANNEL",
    );
}

export async function handleChzzkNavigated(event: ChzzkEvent): Promise<void> {
    const { url, socket } = event;

    console.log("handleChzzkNavigated", event);
    if (!socket.connected) return;

    if (isChzzkLiveUrl(url)) {
        try {
            const liveId = url.replace("https://chzzk.naver.com/live/", "");
            const liveInfo = await getLiveInfo(liveId);
            const channelInfo = await getChannelInfo(liveId);
            if (!liveInfo || !channelInfo) return;

            const presence = {
                "smallImage": true,
                url,
                "details": `'${liveInfo.liveTitle}' 방송 보는 중`,
                "profileImageUrl": channelInfo.channelImageUrl,
                "streamer": channelInfo.channelName,
                "statusDisplay": "state" as const
            };

            await chrome.storage.session.set({ presence });

            socket.send("presence", presence);
        } catch (error) {
            console.error("handleChzzkNavigated", error);
        }

        return;
    }

    if (isChzzkClipUrl(url)) {
        try {
            const clipId = url.replace("https://chzzk.naver.com/clips/", "");
            const clipInfo = await getClipInfo(clipId);
            if (!clipInfo) return;

            const presence = {
                "smallImage": true,
                url,
                "details": `'${clipInfo.clipTitle}' 클립 보는 중`,
                "profileImageUrl": clipInfo.optionalProperty.ownerChannel.channelImageUrl,
                "streamer": clipInfo.optionalProperty.ownerChannel.channelName,
                "statusDisplay": "state" as const
            };

            await chrome.storage.session.set({ presence });

            socket.send("presence", presence);
        } catch (error) {
            console.error("handleChzzkNavigated", error);
        }

        return;
    }

    return await handleChzzkEntered(event);
}
