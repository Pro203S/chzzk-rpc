import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect } from 'react';
import Header from '../../components/header';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faExclamationCircle } from '@fortawesome/free-solid-svg-icons';

export default function Main() {
    const socket = useSocket();

    useEffect(() => {
        (async () => {
            
        })();
    }, []);

    return <div className={css.container}>
        <Header />
        {(socket.error || !socket.connected) && <div className={css.error}>
            <FontAwesomeIcon icon={faExclamationCircle} />
            <div className={css.texts}>
                <span className={css.main}>서버에 연결할 수 없어요.</span>
                <a className={css.sub} href="https://github.com/Pro203S/chzzk-rpc/blob/main/HELP.md" target='_blank'>도움이 필요하신가요?</a>
            </div>
        </div>}
        <div className={css.discord}>
            <span style={{ "color": "#fff" }}>{JSON.stringify(socket)}</span>
        </div>
    </div>;
}
