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

async function getLiveInfo(tabId: number, liveId: string): Promise<LiveResponse> {
    const results = await chrome.scripting.executeScript({
        "target": {
            tabId
        },
        "func": async (liveId: string) => await (await fetch(`https://api.chzzk.naver.com/polling/v3.1/channels/${liveId}/live-status`)).json(),
        "args": [liveId]
    });

    return results[0]?.result?.content;
}

async function getChannelInfo(tabId: number, liveId: string): Promise<ChannelResponse> {
    const results = await chrome.scripting.executeScript({
        "target": {
            tabId
        },
        "func": async (liveId: string) => await (await fetch(`https://api.chzzk.naver.com/service/v1/channels/${liveId}`)).json(),
        "args": [liveId]
    });

    return results[0]?.result?.content;
}

async function getClipInfo(tabId: number, clipId: string): Promise<ClipResponse> {
    const results = await chrome.scripting.executeScript({
        "target": {
            tabId
        },
        "func": async (liveId: string) => await (await fetch(`https://api.chzzk.naver.com/service/v1/clips/${liveId}/detail?optionalProperties=MAKER_CHANNEL&optionalProperties=OWNER_CHANNEL`)).json(),
        "args": [clipId]
    });

    return results[0]?.result?.content;
}

export async function handleChzzkNavigated(event: ChzzkEvent): Promise<void> {
    const { tabId, url, socket } = event;

    console.log("handleChzzkNavigated", event);
    if (!socket.connected) return;

    if (isChzzkLiveUrl(url)) {
        try {
            const liveId = url.replace("https://chzzk.naver.com/live/", "");
            const liveInfo = await getLiveInfo(tabId, liveId);
            const channelInfo = await getChannelInfo(tabId, liveId);
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
            const clipInfo = await getClipInfo(tabId, clipId);
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
