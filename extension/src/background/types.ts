export type ChzzkEventReason = "navigation" | "tab-closed";

export interface ChzzkEvent {
    tabId: number;
    url: string;
    previousUrl?: string;
    reason: ChzzkEventReason;
}
