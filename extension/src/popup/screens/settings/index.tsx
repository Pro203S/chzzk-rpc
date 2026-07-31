import css from './page.module.css';
import useSocket from '../../lib/ws/useSocket';
import { useEffect, useState } from 'react';
import Header from '../../components/header';
import { loadPort, savePort } from '../../../shared/port';
import { FontAwesomeIcon } from '@fortawesome/react-fontawesome';
import { faRefresh } from '@fortawesome/free-solid-svg-icons';

export default function Settings() {
    const socket = useSocket();
    const [port, setPort] = useState<string>();
    const [portError, setPortError] = useState<string>();

    useEffect(() => {
        (async () => {
            setPort(String(await loadPort()));
        })();
    }, []);

    return <div className={css.container}>
        <Header />
        <div className={css.linearV}>
            {port !== undefined && <div className={css.section}>
                <div className={css.row}>
                    <span className={css.title}>포트 설정</span>
                    <div className={css.section}>
                        <input
                            className={css.input}
                            type="text"
                            value={port}
                            placeholder="포트 번호"
                            onChange={async ev => {
                                const { value } = ev.currentTarget;
                                const num = Number(value);
                                if (isNaN(num)) {
                                    setPortError("숫자를 입력해주세요!");
                                    return setPort(value);
                                }

                                if (num < 1023 || num > 65535) {
                                    setPortError("1024-65535 사이의 값을 입력해주세요!");
                                    return setPort(value);
                                }

                                setPortError(undefined);
                                await savePort(num);
                                setPort(String(await loadPort()));
                            }}
                        />
                        <span className={css.sub}>기본값: 58127</span>
                        {portError && <span className={css.error}>{portError}</span>}
                    </div>
                </div>
            </div>}
            <div className={css.section}>
                <div className={css.row}>
                    <span className={css.title}>재연결</span>
                    <button
                        className={css.button}
                        onClick={async () => socket.reconnect(await loadPort())}
                    >
                        <FontAwesomeIcon icon={faRefresh} />
                    </button>
                </div>
                {socket.error && <span className={css.error}>{socket.error}</span>}
                {socket.socketError && <span className={css.error}>{socket.socketError}</span>}
            </div>
        </div>
    </div>;
}
