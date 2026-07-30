import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect, useState } from 'react';
import Header from '../../components/header';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faExclamationCircle } from '@fortawesome/free-solid-svg-icons';
import { DiscordUser, PresencePayload } from '../../../shared/socket';

export default function Main() {
    const socket = useSocket();
    const [error, setError] = useState<string>();
    const [user, setUser] = useState<DiscordUser>();
    const [presence, setPresence] = useState<PresencePayload | null>(null);

    useEffect(() => {
        if (!socket.connected) {
            setError("서버에 연결할 수 없어요.");
            return;
        }

        if (socket.socketError === "-1") {
            setError("Discord를 켜주세요.");
            return;
        }

        if (socket.error) {
            setError("오류: " + socket.error);
            return;
        }

        socket.user().then(v => v && setUser(v));



        setError(undefined);

    }, [socket]);

    useEffect(() => {
        (async () => {
            const { presence } = await chrome.storage.session.get<{ "presence"?: PresencePayload | null }>("presence");
            if (presence === undefined) return;

            setPresence(presence);
        })();

        const cb = (changes: { [key: string]: { newValue?: unknown; oldValue?: unknown; } }) => {
            const { presence } = changes;
            console.log(presence);
            if (!presence || !presence.newValue) return;

            setPresence(presence.newValue as any);
        };
        chrome.storage.session.onChanged.addListener(cb);

        return () => chrome.storage.session.onChanged.removeListener(cb);
    }, []);

    return <div className={css.container}>
        <Header />
        {error && <div className={css.error}>
            <FontAwesomeIcon icon={faExclamationCircle} />
            <div className={css.texts}>
                <span className={css.main}>{error}</span>
                <a className={css.sub} href="https://github.com/Pro203S/chzzk-rpc/blob/main/HELP.md" target='_blank'>도움이 필요하신가요?</a>
            </div>
        </div>}
        {user && <div className={css.user}>
            <img
                src={user.avatarUrl}
                className={css.profile}
                draggable={false}
            />
            <div className={css.texts}>
                <span className={css.main}>{user.displayName}</span>
                <span className={css.sub}>{user.username}</span>
            </div>
        </div>}
        <span style={{ "color": "white" }}>{JSON.stringify(user)}</span>
        <span style={{ "color": "white" }}>{JSON.stringify(presence)}</span>
    </div>;
}
