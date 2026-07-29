import { createRoot } from "react-dom/client";
import "./styles.css";
import { useState } from "react";
import { NavigateContext, ScreenName } from "./lib/context/useNavigate";
import Main from "./screens/main";
import Settings from "./screens/settings";

function App() {
    const [screen, setScreen] = useState<ScreenName>("main");

    return <NavigateContext.Provider value={setScreen}>
        {(() => {
            switch (screen) {
                case "main": return <Main />;
                case "settings": return <Settings />;
            }
        })()}
    </NavigateContext.Provider>
}

createRoot(document.getElementById("root")!).render(<App />);
