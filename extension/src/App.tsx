function DischeeseLogo() {
    return (
        <svg
            aria-hidden="true"
            className="brand__logo"
            viewBox="0 0 32 32"
            fill="none"
            xmlns="http://www.w3.org/2000/svg"
        >
            <path
                d="M7 5.5h18v14H14.4L9 25v-5.5H7v-14Z"
                fill="currentColor"
                stroke="currentColor"
                strokeLinejoin="round"
                strokeWidth="3"
            />
            <path
                d="M12 11h8M12 15h5"
                stroke="#101312"
                strokeLinecap="round"
                strokeWidth="2"
            />
        </svg>
    );
}

function App() {
    return (
        <main className="popup">
            <header className="header">
                <div className="brand">
                    <DischeeseLogo />
                    <div>
                        <h1>Discheese</h1>
                        <p>CHZZK Rich Presence</p>
                    </div>
                </div>
                <span className="version">v0.1.0</span>
            </header>

            <section className="status-card" aria-labelledby="connection-title">
                <div className="status-card__top">
                    <div>
                        <p className="eyebrow">연결 상태</p>
                        <h2 id="connection-title">치지직에서 동작 중</h2>
                    </div>
                    <span className="status-dot" aria-label="정상" />
                </div>
                <p className="status-card__description">
                    현재 탭의 치지직 활동을 감지하고 있습니다.
                </p>
            </section>

            <footer>
                <p>Discheese는 chzzk.naver.com에서만 활성화됩니다.</p>
            </footer>
        </main>
    );
}

export default App;
