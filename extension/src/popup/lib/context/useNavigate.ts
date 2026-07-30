import { createContext, useContext } from 'react';

export type ScreenName = "main" | "settings";

export const NavigateContext = createContext<[React.Dispatch<React.SetStateAction<ScreenName>>, ScreenName] | null>(null);

export default function useNavigate() {
    const ctx = useContext(NavigateContext);
    if (!ctx) {
        throw new Error('Not wrapped by Provider (' + useNavigate.name + ")");
    }
    return ctx;
}